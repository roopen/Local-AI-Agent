import type { AxiosInstance, AxiosProgressEvent } from 'axios';
import { readArticle } from './ArticleReaderClient';
import { request } from './UserApiClient/core/request';
import { CancelablePromise } from './UserApiClient/core/CancelablePromise';
import { articleReaderErrorMessage } from './ArticleReaderError';

jest.mock('./UserApiClient/core/request');

function stream() {
    let finish!: (text: string) => void;
    const cancelled = jest.fn();
    const pending = new CancelablePromise<unknown>((resolve, _reject, onCancel) => {
        finish = resolve;
        onCancel(cancelled);
    });
    jest.mocked(request).mockReturnValueOnce(pending);
    const progress = jest.fn();
    const result = readArticle('https://example.com/article', progress);
    const calls = jest.mocked(request).mock.calls;
    const transport = calls[calls.length - 1][2] as AxiosInstance;
    const receive = (text: string) => transport.defaults.onDownloadProgress!({
        event: { target: { responseText: text } },
    } as AxiosProgressEvent);
    return { result, progress, receive, finish, cancelled };
}

test('reports complete progress records once across fragmented delivery and resolves the final result', async () => {
    const s = stream();
    const first = '{"type":"progress","progress":{"phase":"extracting"}}\n';
    s.receive(first.slice(0, 20));
    expect(s.progress).not.toHaveBeenCalled();
    s.receive(first + '{"type":"res');
    expect(s.progress).toHaveBeenCalledWith({ phase: 'extracting' });
    s.finish(first + '{"type":"result","result":{"original":{"title":"Article"}}}\n');
    await expect(s.result).resolves.toMatchObject({ original: { title: 'Article' } });
    expect(s.progress).toHaveBeenCalledTimes(1);
});

test('streamed setup errors retain the reader error message', async () => {
    const s = stream();
    s.finish('{"type":"error","status":503,"code":"article_browser_unreachable","requestId":"request-123","phase":"opening","diagnostic":"Connection refused"}\n');
    const error = await s.result.catch(value => value);
    expect(articleReaderErrorMessage(error)).toContain('could not be reached');
    expect(error).toMatchObject({ body: { requestId: 'request-123', phase: 'opening', diagnostic: 'Connection refused' } });
});

test('a truncated stream never resolves an incomplete article', async () => {
    const s = stream();
    s.finish('{"type":"progress","progress":{"phase":"opening"}}\n{"type":"result"');
    await expect(s.result).rejects.toThrow('before completion');
});

test('cancellation stops the transport and ignores late progress', async () => {
    const s = stream();
    const rejection = expect(s.result).rejects.toThrow('Request aborted');
    s.result.cancel();
    s.receive('{"type":"progress","progress":{"phase":"translating"}}\n');
    await rejection;
    expect(s.cancelled).toHaveBeenCalledTimes(1);
    expect(s.progress).not.toHaveBeenCalled();
});
