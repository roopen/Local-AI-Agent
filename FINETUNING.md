# Fine-tuning the Local-AI-Agent LLM

This guide describes how to fine-tune the LLM used by Local-AI-Agent for its two LLM workloads:

1. **News recommendation** — a per-user relevancy classifier that tags each article `High`/`Low` with a topic label.
2. **News translation** — a JSON-in / JSON-out translator that translates an article's `title` + `summary` into the user's target language.

Both workloads run through the same `IChatClient` (LM Studio / Ollama / Azure OpenAI compatible). Fine-tuning replaces the base model with one trained on your own preference and translation history so that the model: requires fewer tokens, conforms more reliably to the exact output format, and reproduces your specific likes/dislikes without needing a long system prompt at inference time.

---

## 1. How the data is generated (read this first)

The application already builds a chat-format dataset for you while you use it. You don't need to write a separate exporter — just use the app, then download the produced ZIP.

### News pipeline (recommendation)

| Step | Code | Notes |
|---|---|---|
| Fetch RSS/Atom feeds | `LocalAIAgent.Application/News/NewsService.cs:51` (`LoadAllNews`) | 48+ built-in feeds via `BaseNewsClientSettings` subclasses plus user custom feeds via `CustomFeedFetcher.FetchAsync` (`CustomFeedFetcher.cs:15`). Each item gets a `Language` (BCP-47) tag from the feed settings. |
| Build `NewsItem` | `LocalAIAgent.Application/News/NewsItem.cs:41` | Captures `Title`, `Summary`, `Link`, `Source` (DNS host), `Categories`, `Language`, `SourceClientName`. HTML is stripped and decoded. |
| Filter by user prefs | `NewsService.FilterNews` (`NewsService.cs:111`) | Drops articles older than 24h, articles from disabled feeds, and articles whose title/summary/category whole-word-matches any entry in `UserPreferences.Dislikes`. |
| Evaluate with LLM | `LocalAIAgent.Application/News/AI/EvaluateNewsUseCase.cs:37` (`EvaluateCoreAsync`) | Batches of 3. System prompt is `UserPreferences.BuildSystemPrompt()` (`LocalAIAgent.Domain/UserPreferences.cs:30`). LLM emits a `<\|think\|>` block + JSON array of `{ArticleIndex, Relevancy, Topic}`. Cached evaluations are reused. |
| Persist | `NewsDatasetRepository.SaveAsync` (`LocalAIAgent.API/Infrastructure/NewsDatasetRepository.cs:27`) → `NewsEvaluationEntry` table | Saves `Title`, `Summary`, `Link`, `Source`, `Topic`, `Relevancy`, optional `Reasoning`, `UseInDataset` flag (controlled by the LLM option's **Save results to dataset** setting), `ModelUsed`, `UserPreferencesId`. |
| User feedback | `NewsController.SubmitFeedback` (`LocalAIAgent.API/Api/Controllers/NewsController.cs`) | Like/dislike clicks create/update the same `NewsEvaluationEntry` rows — this is your highest-quality label source. |

### Translation pipeline

| Step | Code | Notes |
|---|---|---|
| Decide what to translate | `GetTranslationUseCase.TranslateArticleAsync` (`LocalAIAgent.Application/News/AI/GetTranslationUseCase.cs:36`) | Only articles where `SourceLanguage != UserPreferences.TargetLanguage`. |
| Cache lookup | `IArticleTranslationRepository.GetCachedTranslationsAsync` | Hits go straight to the article, no LLM call. |
| Translate | `TranslateBatchWithFallbackAsync` (`GetTranslationUseCase.cs`) | Batches of up to 5 articles. The short system prompt disables translation-time reasoning, and LM Studio constrains the response to `[{index,title,summary}]`. Valid indexed results are kept while only missing items are retried; total failures are split into smaller batches. |
| Robust parsing | `SanitizeJsonResponse` (line 213) | Regex extracts the JSON array; a small state machine repairs unescaped quotes and `\'`. |
| Persist | `translationRepository.SaveTranslationsAsync` (line 174) | Only when **Save results to dataset** is enabled for the selected LLM option. Stores `ArticleLink`, `OriginalTitle`, `OriginalSummary`, `TranslatedTitle`, `TranslatedSummary`, `TargetLanguage`, `CreatedAt`. |

### Dataset export (the file you fine-tune on)

Entry point: `LocalAIAgent.API/Application/UseCases/GetDatasetUseCase.cs` (`GetDatasetZipAsync`). HTTP endpoint: `GET /api/News/Dataset` (`NewsController.GetDataset`), available to authenticated owners.

To export evaluations produced by one model, use `GET /api/News/Dataset?modelId=your-model-id` and URL-encode the model ID. The filter matches the stored `ModelUsed` exactly (case-sensitive), after trimming surrounding whitespace. Filtered exports exclude translation samples because translations do not store the producing model; no matches returns HTTP 404. Omitting `modelId`, or leaving it blank, keeps the combined export across all models.

Only evaluations marked `UseInDataset = true` are exported. The owner-only `GET /api/News/Dataset/Models` endpoint lists their distinct model IDs.

The export produces a ZIP containing two JSONL files in OpenAI chat-completions format:

```jsonl
{"messages":[{"role":"system","content":"…"},{"role":"user","content":"…"},{"role":"assistant","content":"…"}]}
```

- **Recommendation samples** (`GetBalancedNewsEntries`, line 96): system = `UserPreferences.BuildSystemPrompt()`; user = `FormatKnownTopics(...)` + 1–3 articles joined by `---ARTICLE SEPARATOR---`; assistant = optional `<|think|>…<|end|>` block + JSON `[{ArticleIndex, Relevancy, Topic}]`. The exporter prefers the *original* (untranslated) text via `translationsByLink` so the recommender trains on source language, matching how it's called at inference.
- **Translation samples** (lines 38–55): system = the same prompt `GetTranslationUseCase.GetSystemPrompt(targetLang)` used at inference; user = `"Translate every item. Input JSON:"` + serialized `[{index, title, summary}]`; assistant = serialized `[{index, title, summary}]`.
- **Balancing**: news entries are sampled to roughly equal the translation batch count, then both pools are shuffled together. Eval split is `clamp(translation_share, 15%, 30%)`.
- **Output**: `training_dataset.jsonl` and `evaluation_dataset.jsonl` inside `dataset.zip`.

---

## 2. Prerequisites

1. Use the app for real for at least a week with **Save results to dataset** enabled in **Settings → LLM**. Without this flag, translations are not persisted and evaluations are flagged `UseInDataset = false`. Target ≥1000 evaluations and ≥300 translation pairs (per target language) before training — fewer can fine-tune a tiny model but won't beat the base on a 7B+.
2. **Give honest feedback in the UI.** Like/dislike clicks overwrite the LLM's guess on `NewsEvaluationEntry.Relevancy`. Those rows are the gold labels — the more you click, the more the recommender will resemble *your* taste rather than the bootstrap model's.
3. **Don't mutate an existing `UserPreferences` row mid-collection.** Different users with different prompts/interests/dislikes are *desirable* — that variation is what teaches the model to condition on the system prompt instead of memorizing one taste. The narrow problem is in-place edits to a single row: `GetBalancedNewsEntries` calls `BuildSystemPrompt()` on the *current* preferences at export time (`GetDatasetUseCase.cs:141`), so every historical `NewsEvaluationEntry` joined by `UserPreferencesId` gets re-paired with the new prompt text — including labels that were produced under the old prompt. If you need to change your preferences, prefer creating a new `UserPreferences` row (new `Id`) so old rows stay attached to the prompt they were actually judged under, or filter out evaluations older than the edit before training.
4. **Pick a base model that already runs in your LM Studio / Ollama.** Recommendation samples use reasoning markers, while translation samples use a concise indexed JSON contract. Recommended starting points: a 4B–8B instruct model for the recommender (Qwen2.5-7B-Instruct, Llama-3.1-8B-Instruct, Gemma-2-9B), and the same or larger for translation if your target language is non-Latin.

---

## 3. Producing the dataset

1. As the owner, open **Settings → LLM**, edit the LLM option, enable **Save results to dataset**, and choose **Test and save**. This setting is stored per option and applies immediately; new and migrated options start with collection off. Turning it off affects new results, while previously collected data remains available. Evaluations cached while collection was off stay excluded.
2. Use the app normally for the data-collection period.
3. In **Settings → LLM → Training dataset**, choose **All models** or one recorded model and click **Download dataset**. The model list includes eligible historical evaluations, even if the LLM option was deleted. You can also download through the owner-only API:
   ```
   GET http://localhost:<port>/api/News/Dataset
   → dataset.zip
   ```
4. Unzip. You will get `training_dataset.jsonl` and `evaluation_dataset.jsonl`.
5. **Sanity check** before training:
   - Each line must be valid JSON with exactly three messages in order: `system`, `user`, `assistant`.
   - Recommender assistant content must end with a parseable JSON array; translation assistant content must *be* a parseable JSON array.
   - Reject any row where the assistant's JSON array length doesn't match the number of articles in the user message.
   - Strip duplicate `ArticleLink`s across the two splits (the exporter shuffles after combining, so the same article should not appear in both splits but verify).
   - Check class balance: if `High` is <15% or >70% of recommender rows, oversample / weight loss during training.

A 20-line pandas/jq filter is enough — no extra tooling is needed.

---

## 4. Fine-tuning recipes

The dataset is plain OpenAI-style chat JSONL, so it works as-is with most fine-tuning stacks. Two recommended paths:

### A. Local LoRA on a single GPU (recommended for this project)

Tooling: [Unsloth](https://github.com/unslothai/unsloth) or [Axolotl](https://github.com/axolotl-ai-cloud/axolotl) with QLoRA.

Hyperparameters that match this dataset's shape (short prompts, structured JSON output, 3-article batches):

| Param | Value | Why |
|---|---|---|
| Rank `r` | 16–32 | Two small skills (format adherence + your taste); larger ranks overfit. |
| Alpha | `2 * r` | Standard. |
| Target modules | `q_proj,k_proj,v_proj,o_proj` (+ `gate_proj,up_proj,down_proj` for translation) | Translation needs MLP capacity for vocab shift; the recommender does not. |
| LR | 1e-4 (QLoRA) / 2e-5 (full FT) | Lower for translation. |
| Sequence length | 4096 | Articles + 3-batch user message rarely exceed 2k; 4k leaves headroom. |
| Epochs | 2–3 | The dataset has duplicated patterns (3-article batches with similar structure) — more than 3 epochs and the model starts memorizing summaries. |
| Loss masking | Mask system + user, train on assistant only | Critical. Otherwise the model learns to recite your system prompt. Both Unsloth and Axolotl support this via `train_on_responses_only` / `train_on_inputs: false`. |
| Train/eval split | Use the files as-shipped | Already split with deduplication. |

### B. Two adapters vs one

The dataset mixes both tasks. You have two reasonable options:

1. **One adapter, mixed training** — simplest. The base model learns both behaviors from the role of the system prompt. Recommended unless evaluation shows interference.
2. **Two adapters** — split the JSONL by whether the system prompt starts with `"Evaluate the following news articles"` (recommender) or `"Translate every news item into"` (translator), train each separately, then load whichever adapter the runtime needs. More work, but gives sharper specialization for translation in particular.

Either way: ship the result as a GGUF (`llama.cpp` / Unsloth `save_pretrained_gguf`) so LM Studio can load it directly.

---

## 5. Wiring the fine-tuned model back in

1. Quantize to Q4_K_M or Q5_K_M GGUF, load it in LM Studio, give it a clear name like `local-ai-agent-recommender-q4`.
2. As the owner, add or edit an option in **Settings → LLM** with model ID `local-ai-agent-recommender-q4` and the LM Studio endpoint, then **Test and save**.
3. Leave **Save results to dataset** **off** for a while — you don't want the new model's outputs poisoning the next dataset until you confirm it's at least as good as the base. Re-enable once you trust it (and rotate datasets so you can train v2 only on rows the new model produced + your feedback corrections).
4. Verify both pipelines end-to-end:
   - `EvaluateNewsUseCase` should still produce a JSON array of length 3 per 3-article batch. The deserializer in `EvaluationResult.Deserialize` is strict — a malformed array means the fine-tune broke the format. If this happens, lower the LR and retrain.
   - `GetTranslationUseCase.SanitizeJsonResponse` is forgiving (handles `\'`, unescaped inner quotes). But translation regressions usually show as truncated summaries — spot-check 20 articles across two languages before declaring success.

---

## 6. Evaluation

The evaluation JSONL is a holdout. Measure both metrics offline before swapping in production:

- **Recommender**: per-row, parse the assistant JSON and compare `Relevancy` against ground truth. Report precision/recall for the `High` class — that's the user-facing one. A 5-point bump in `High`-recall over the base model is the bar for shipping.
- **Translator**: BLEU/chrF against the gold `assistant` content is fine for a sanity number, but the structural check matters more: how often is the response a valid JSON array with one unique, matching index per input? A fine-tune should hit 99%+.

---

## 7. Common pitfalls specific to this codebase

- **In-place edits to a `UserPreferences` row retroactively re-label history.** `BuildSystemPrompt` runs at export time against the *current* row, while assistant labels were produced against the row's *then-current* values. Across separate users this is fine and even helpful (varied system prompts → the model learns to condition on them). Within one user, in-place edits create rows whose system prompt contradicts the saved label. Filter by `NewsEvaluationEntry.CreatedAt > <last edit>` before training, or create a new `UserPreferences` row instead of mutating.
- **`#if DEBUG` `Reasoning` field.** The system prompt and dataset include a `Reasoning` field only in Debug builds (`UserPreferences.cs:56`, `EvaluateNewsUseCase.cs:243`). Train and serve in the *same* build configuration or strip the field manually from the JSONL.
- **Do not add a `<|think|>` block to translation samples.** Translation uses schema-constrained JSON directly because reasoning tokens add latency and create another failure point. Reasoning remains useful for the recommender, where humans inspect it in Debug.
- **24-hour news cutoff.** `NewsService.FilterNews` drops articles older than 24h at *inference* time. The dataset has no such cutoff — historical evaluations stay forever, which is what you want for training. Just don't compute "model accuracy this week" from the eval JSONL alone; it's a static snapshot.
- **Language coverage.** Translation rows only exist for languages you actually used `TargetLanguage` for. If you fine-tune on en-only data, the model's other-language fluency degrades. Either keep one adapter per language or use option B above.
