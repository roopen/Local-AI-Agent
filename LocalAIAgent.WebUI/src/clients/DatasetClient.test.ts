import { downloadDataset } from './DatasetClient';

jest.mock('./UserApiClient', () => ({ OpenAPI: { BASE: '' }, NewsService: { getDatasetModels: jest.fn() } }));

describe('dataset downloads', () => {
    const fetchMock = jest.fn();
    const originalFetch = global.fetch;
    const originalCreateObjectURL = URL.createObjectURL;
    const originalRevokeObjectURL = URL.revokeObjectURL;

    beforeEach(() => {
        jest.useFakeTimers();
        global.fetch = fetchMock;
        fetchMock.mockReset();
        URL.createObjectURL = jest.fn().mockReturnValue('blob:dataset');
        URL.revokeObjectURL = jest.fn();
    });

    afterEach(() => {
        jest.runOnlyPendingTimers();
        jest.useRealTimers();
        jest.restoreAllMocks();
        global.fetch = originalFetch;
        URL.createObjectURL = originalCreateObjectURL;
        URL.revokeObjectURL = originalRevokeObjectURL;
    });

    it.each([undefined, 'publisher/model:4b+q8'])('downloads the binary archive with model filter %s', async modelId => {
        const archive = new Blob(['PK\x03\x04'], { type: 'application/zip' });
        fetchMock.mockResolvedValue({ ok: true, blob: async () => archive });
        const click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
            expect(this.download).toBe('dataset.zip');
            expect(this.href).toBe('blob:dataset');
        });
        await downloadDataset(modelId);
        const requested = new URL(fetchMock.mock.calls[0][0], 'http://localhost');
        expect(requested.searchParams.get('modelId')).toBe(modelId ?? null);
        expect(fetchMock.mock.calls[0][1].credentials).toBe('include');
        expect(URL.createObjectURL).toHaveBeenCalledWith(archive);
        expect(click).toHaveBeenCalledTimes(1);
        expect(document.querySelector('a[download]')).toBeNull();
        jest.runOnlyPendingTimers();
        expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:dataset');
    });

    it.each([401, 403, 404, 500])('does not download an error response (%s)', async status => {
        fetchMock.mockResolvedValue({ ok: false, status });
        await expect(downloadDataset('model')).rejects.toThrow();
        expect(URL.createObjectURL).not.toHaveBeenCalled();
    });
});
