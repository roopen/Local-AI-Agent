import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import ArticleReaderModal from './ArticleReaderModal';
import NewsComponent from './NewsComponent';
import type NewsArticle from '../domain/NewsArticle';
import { readArticle, type ReadArticleResult } from '../clients/ArticleReaderClient';
import { NewsStreamClient } from '../clients/NewsStreamingClient';

jest.mock('../clients/ArticleReaderClient');
jest.mock('../clients/NewsStreamingClient', () => {
    const stream = { start: jest.fn(), stop: jest.fn(), isLoading: true };
    return { NewsStreamClient: { getInstance: () => stream }, LlmConnectionError: class extends Error {} };
});
jest.mock('../users/UserService', () => ({ __esModule: true, default: { getInstance: () => ({ getCurrentUser: () => null }) } }));

const first = { Title: 'First article', Link: 'https://example.com/first', Summary: 'First summary', Source: 'Example' } as NewsArticle;
const second = { ...first, Title: 'Second article', Link: 'https://example.com/second' };
const result: ReadArticleResult = {
    original: { title: 'Original title', markdown: 'Original paragraph', sourceUrl: first.Link, status: 'complete' },
    translatedTitle: 'Translated title', translatedMarkdown: 'Translated paragraph', targetLanguage: 'en', translationStatus: 'complete',
};

function deferred() {
    let resolve!: (value: ReadArticleResult) => void;
    let reject!: (reason: Error) => void;
    const promise = Object.assign(new Promise<ReadArticleResult>((res, rej) => { resolve = res; reject = rej; }), { cancel: jest.fn() });
    jest.mocked(readArticle).mockReturnValueOnce(promise as unknown as ReturnType<typeof readArticle>);
    return { resolve, reject, promise };
}

beforeEach(() => {
    jest.clearAllMocks();
    Object.defineProperty(window, 'matchMedia', { writable: true, value: jest.fn(() => ({ matches: false, addEventListener: jest.fn(), removeEventListener: jest.fn() })) });
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
});

test('loading animation follows real phases and ignores progress from a previous article', () => {
    deferred();
    deferred();
    const view = render(<ArticleReaderModal article={first} onClose={jest.fn()} />);
    expect(screen.getByRole('status').textContent).toContain('Connecting');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByRole('complementary', { name: 'Full article request' })).toBeTruthy();
    const firstProgress = jest.mocked(readArticle).mock.calls[0][1]!;
    act(() => firstProgress({ phase: 'translating', completed: 2, total: 5 }));
    expect(screen.getByRole('status').textContent).toContain('2 of 5 sections translated');
    expect(screen.getByLabelText('Article loading phases').querySelector('[aria-current="step"]')?.textContent).toContain('Translating');
    expect(screen.queryByRole('dialog')).toBeNull();
    view.rerender(<ArticleReaderModal article={second} onClose={jest.fn()} />);
    act(() => firstProgress({ phase: 'translating', completed: 4, total: 5 }));
    expect(screen.getByRole('status').textContent).toContain('Connecting');
});

test('late response for an earlier selection cannot overwrite the current article', async () => {
    const a = deferred();
    const b = deferred();
    const view = render(<ArticleReaderModal article={first} onClose={jest.fn()} />);
    view.rerender(<ArticleReaderModal article={second} onClose={jest.fn()} />);
    expect(a.promise.cancel).toHaveBeenCalledTimes(1);
    await act(async () => b.resolve({ ...result, translatedTitle: 'Current selection' }));
    await act(async () => a.resolve(result));
    expect(screen.getByRole('heading').textContent).toBe('Current selection');
});

test('shows translated text without an original toggle, rejects unsafe content, and handles Escape', async () => {
    const pending = deferred();
    const close = jest.fn();
    render(<ArticleReaderModal article={first} onClose={close} />);
    await act(async () => pending.resolve({ ...result, translatedMarkdown: 'Translated paragraph\n\n<script>alert(1)</script>\n\n[bad](javascript:alert)\n\n![tracking](https://tracker.test/pixel)' }));
    expect(screen.getByText('Translated paragraph')).toBeTruthy();
    expect(document.querySelector('script, img')).toBeNull();
    expect(screen.getByText('bad').getAttribute('href')).toBeNull();
    expect(screen.queryByRole('button', { name: /Show original|Show translation/ })).toBeNull();
    expect(screen.queryByText('Original paragraph')).toBeNull();
    fireEvent(screen.getByRole('dialog'), new Event('cancel', { bubbles: true, cancelable: true }));
    expect(close).toHaveBeenCalledTimes(1);
});

test('reader opening, failure, retry and close leave the feed running and retain incoming articles', async () => {
    const pending = deferred();
    const retry = deferred();
    const globalError = jest.fn();
    const stream = NewsStreamClient.getInstance();
    const view = render(<NewsComponent onLlmConnectionFailure={globalError} />);
    const onArticle = jest.mocked(stream.start).mock.calls[0][0];
    act(() => onArticle(first));
    fireEvent.click(screen.getByRole('button', { name: 'Read in app' }));
    act(() => onArticle(second));
    expect(screen.getByText('Second article')).toBeTruthy();
    for (const button of screen.getAllByRole('button', { name: 'Read in app' })) {
        expect((button as HTMLButtonElement).disabled).toBe(true);
        fireEvent.click(button);
    }
    expect(readArticle).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).toBeNull();
    await act(async () => pending.reject(new Error('reader failed')));
    expect(screen.getByRole('alert')).toBeTruthy();
    expect(screen.queryByRole('dialog')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Retry article' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    await act(async () => retry.resolve(result));
    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(screen.queryByRole('complementary')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Close article' }));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Read in app' }).every(button => !(button as HTMLButtonElement).disabled)).toBe(true);
    expect(screen.getByText('First article')).toBeTruthy();
    expect(screen.getByText('Second article')).toBeTruthy();
    expect(stream.start).toHaveBeenCalledTimes(1);
    expect(stream.stop).not.toHaveBeenCalled();
    expect(globalError).not.toHaveBeenCalled();
    view.unmount();
    expect(stream.stop).toHaveBeenCalledTimes(1);
});

test('cancelling a pending article allows another request and ignores the cancelled result', async () => {
    const pending = deferred();
    const next = deferred();
    render(<NewsComponent />);
    const onArticle = jest.mocked(NewsStreamClient.getInstance().start).mock.calls[0][0];
    act(() => { onArticle(first); onArticle(second); });
    fireEvent.click(screen.getAllByRole('button', { name: 'Read in app' })[0]);
    fireEvent.click(screen.getByRole('button', { name: 'Close article' }));
    expect(pending.promise.cancel).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getAllByRole('button', { name: 'Read in app' })[1]);
    expect(readArticle).toHaveBeenLastCalledWith(second.Link, expect.any(Function));
    await act(async () => pending.resolve(result));
    expect(screen.queryByRole('dialog')).toBeNull();
    await act(async () => next.resolve(result));
    expect(screen.getByRole('dialog')).toBeTruthy();
});

test('unmounting a pending request cancels it without moving focus', async () => {
    const pending = deferred();
    const opener = document.createElement('button');
    document.body.append(opener);
    opener.focus();
    const view = render(<ArticleReaderModal article={first} onClose={jest.fn()} />);
    view.unmount();
    expect(pending.promise.cancel).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(opener);
    await act(async () => pending.resolve(result));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    opener.remove();
});
