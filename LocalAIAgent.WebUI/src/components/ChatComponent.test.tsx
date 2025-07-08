import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import ChatComponent from './ChatComponent';
import { ChatConnection } from '../clients/ChatClient';
import ChatMessage from '../domain/ChatMessage';
import { HubConnectionState } from '@microsoft/signalr';

// Mock the ChatConnection class
jest.mock('../clients/ChatClient');

// Mock react-markdown and remark-gfm
jest.mock('react-markdown', () => (props: React.PropsWithChildren<object>) => {
    return <div>{props.children}</div>;
});
jest.mock('remark-gfm', () => () => {});

const mockedChatConnection = ChatConnection as jest.MockedClass<typeof ChatConnection>;

describe('ChatComponent', () => {
    let messageCallback: (message: ChatMessage) => void;
    let mockConnectionInstance: {
        start: jest.Mock;
        stop: jest.Mock;
        onMessageReceived: jest.Mock;
        sendMessage: jest.Mock;
        getState: jest.Mock;
    };

    beforeEach(() => {
        jest.clearAllMocks();

        mockConnectionInstance = {
            start: jest.fn().mockResolvedValue(undefined),
            stop: jest.fn().mockResolvedValue(undefined),
            onMessageReceived: jest.fn().mockImplementation((callback) => {
                messageCallback = callback;
                return jest.fn(); // Return a mock cleanup function
            }),
            sendMessage: jest.fn().mockResolvedValue(undefined),
            getState: jest.fn().mockReturnValue(HubConnectionState.Connected),
        };

        mockedChatConnection.mockImplementation(() => mockConnectionInstance as unknown as ChatConnection);
    });

    it('renders the chat component when connected', () => {
        render(<ChatComponent />);
        expect(screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /send/i })).toBeInTheDocument();
        expect(screen.getByText('No messages yet.')).toBeInTheDocument();
    });

    it('sends a message when the form is submitted', async () => {
        render(<ChatComponent />);
        const inputElement = screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)');
        const sendButton = screen.getByRole('button', { name: /send/i });

        fireEvent.change(inputElement, { target: { value: 'Test message' } });
        fireEvent.click(sendButton);

        await waitFor(() => {
            expect(mockConnectionInstance.sendMessage).toHaveBeenCalledWith('You', 'Test message');
        });

        expect((inputElement as HTMLInputElement).value).toBe('');
    });

    it('displays received messages', () => {
        render(<ChatComponent />);
        const receivedMessage = new ChatMessage('Bot', 'Hello from bot', 'msg2');

        act(() => {
            messageCallback(receivedMessage);
        });

        expect(screen.getByText('Hello from bot')).toBeInTheDocument();
    });

    describe('when connection is not established', () => {
        it('displays connecting status and connects successfully', async () => {
            let resolveStart: () => void;
            const startPromise = new Promise<void>(resolve => {
                resolveStart = resolve;
            });

            mockConnectionInstance.getState.mockReturnValue(HubConnectionState.Disconnected);
            mockConnectionInstance.start.mockReturnValue(startPromise);

            render(<ChatComponent />);

            expect(mockConnectionInstance.start).toHaveBeenCalledTimes(1);
            expect(screen.getByPlaceholderText('Connecting...')).toBeInTheDocument();
            expect(screen.getByRole('button', { name: /send/i })).toBeDisabled();
            expect(screen.getByText('Connecting to chat server...')).toBeInTheDocument();

            mockConnectionInstance.getState.mockReturnValue(HubConnectionState.Connected);

            await act(async () => {
                resolveStart();
                await new Promise(resolve => setTimeout(resolve, 0));
            });

            expect(screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)')).toBeInTheDocument();
            expect(screen.queryByText('Connecting to chat server...')).not.toBeInTheDocument();
        });
    });

    it('stops connection on unmount', () => {
        const { unmount } = render(<ChatComponent />);
        unmount();
        expect(mockConnectionInstance.stop).toHaveBeenCalledTimes(1);
    });
});