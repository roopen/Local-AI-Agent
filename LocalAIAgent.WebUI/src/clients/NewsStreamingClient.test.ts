import {
    extractLlmConnectionFailureMessage,
    LLM_CONNECTION_FAILURE_CODE,
    NewsStreamClient,
    parseNewsLoadingPhase,
} from './NewsStreamingClient';
import { HubConnectionState } from '@microsoft/signalr';

describe('extractLlmConnectionFailureMessage', () => {
    it('extracts a tagged LLM failure from a SignalR error wrapper', () => {
        const error = new Error(
            `Server invocation failed: ${LLM_CONNECTION_FAILURE_CODE}The AI service host could not be resolved.`
        );

        expect(extractLlmConnectionFailureMessage(error))
            .toBe('The AI service host could not be resolved.');
    });

    it('does not classify an ordinary stream error as an LLM connection failure', () => {
        expect(extractLlmConnectionFailureMessage(new Error('SignalR connection closed.')))
            .toBeNull();
    });
});

describe('parseNewsLoadingPhase', () => {
    it.each([
        ['feeds', 'feeds'],
        ['llm', 'llm'],
        ['unknown', null],
        [undefined, null],
    ])('maps %p to %p', (value, expected) => {
        expect(parseNewsLoadingPhase(value)).toBe(expected);
    });
});

describe('NewsStreamClient cancellation', () => {
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('disposes the active stream before stopping the hub connection', async () => {
        const client = NewsStreamClient.getInstance();
        const connection = client.getConnection();
        const dispose = jest.fn();
        const stop = jest.spyOn(connection, 'stop').mockResolvedValue();
        jest.spyOn(connection, 'state', 'get')
            .mockReturnValue(HubConnectionState.Connected);
        (client as unknown as {
            _streamSubscription: { dispose(): void } | null;
        })._streamSubscription = { dispose };

        await client.stop();

        expect(dispose).toHaveBeenCalledTimes(1);
        expect(stop).toHaveBeenCalledTimes(1);
    });

    it('does not create a stream if stopped while the connection is starting', async () => {
        const client = NewsStreamClient.getInstance();
        const connection = client.getConnection();
        const internals = client as unknown as {
            userService: { getCurrentUser(): { id: string; name: string } | null };
        };
        jest.spyOn(internals.userService, 'getCurrentUser')
            .mockReturnValue({ id: '7', name: 'Test user' });

        let state = HubConnectionState.Disconnected;
        jest.spyOn(connection, 'state', 'get').mockImplementation(() => state);

        let resolveStart!: () => void;
        const pendingStart = new Promise<void>(resolve => {
            resolveStart = resolve;
        });
        jest.spyOn(connection, 'start').mockImplementation(() => {
            state = HubConnectionState.Connecting;
            return pendingStart;
        });
        const stop = jest.spyOn(connection, 'stop').mockImplementation(async () => {
            state = HubConnectionState.Disconnected;
        });
        const stream = jest.spyOn(connection, 'stream');

        const starting = client.start(
            jest.fn(),
            jest.fn(),
            jest.fn(),
            jest.fn());
        await client.stop();

        // Simulate a transport whose pending start resolves after stop().
        state = HubConnectionState.Connected;
        resolveStart();
        await starting;

        expect(stream).not.toHaveBeenCalled();
        expect(stop).toHaveBeenCalled();
    });
});
