import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import ChatComponent from './ChatComponent';
import * as ChatClient from '../clients/ChatClient';
import ChatMessage from '../domain/ChatMessage';
import { HubConnectionState } from '@microsoft/signalr';

// Mock the ChatClient module
jest.mock('../clients/ChatClient');

// Cast mocks for type safety
const mockedGetConnection = ChatClient.getConnection as jest.Mock;
const mockedSendMessage = ChatClient.sendMessage as jest.Mock;
const mockedOnMessageReceived = ChatClient.onMessageReceived as jest.Mock;

describe('ChatComponent', () => {
  let messageCallback: (message: ChatMessage) => void;
  const mockConnection = {
    start: jest.fn(),
    stop: jest.fn().mockResolvedValue(undefined),
    on: jest.fn(),
    off: jest.fn(),
    invoke: jest.fn(),
    state: HubConnectionState.Connected, // Default state
  };

  beforeEach(() => {
    jest.clearAllMocks();

    // Default mock implementations
    mockedGetConnection.mockReturnValue(mockConnection);
    mockedSendMessage.mockResolvedValue(undefined);
    mockedOnMessageReceived.mockImplementation((callback) => {
      messageCallback = callback;
      return jest.fn(); // Return a mock cleanup function
    });
    mockConnection.start.mockResolvedValue(undefined); // Default start behavior
    mockConnection.state = HubConnectionState.Connected; // Reset state
  });

  it('renders the chat component when connected', () => {
    render(<ChatComponent />);
    expect(screen.getByPlaceholderText('Type a message...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send/i })).toBeInTheDocument();
    expect(screen.getByText('No messages yet.')).toBeInTheDocument();
  });

  it('sends a message when the form is submitted', async () => {
    render(<ChatComponent />);
    const inputElement = screen.getByPlaceholderText('Type a message...');
    const sendButton = screen.getByRole('button', { name: /send/i });

    fireEvent.change(inputElement, { target: { value: 'Test message' } });
    fireEvent.click(sendButton);

    await waitFor(() => {
      expect(mockedSendMessage).toHaveBeenCalledWith('You', 'Test message');
    });

    expect((inputElement as HTMLInputElement).value).toBe('');
  });

  it('displays received messages', () => {
    render(<ChatComponent />);
    const receivedMessage = new ChatMessage('Bot', 'Hello from bot', 'msg2');

    act(() => {
      messageCallback(receivedMessage);
    });

    expect(screen.getByText('Bot: Hello from bot')).toBeInTheDocument();
  });

  describe('when connection is not established', () => {
    it('displays connecting status and connects successfully', async () => {
      // Mock a controllable promise for the start method
      let resolveStart: () => void;
      const startPromise = new Promise<void>(resolve => {
        resolveStart = resolve;
      });

      // Setup mocks for the disconnected state
      mockConnection.state = HubConnectionState.Disconnected;
      mockConnection.start.mockReturnValue(startPromise);
      
      render(<ChatComponent />);
      
      // Verify initial connecting UI
      expect(mockConnection.start).toHaveBeenCalledTimes(1);
      expect(screen.getByPlaceholderText('Connecting...')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /send/i })).toBeDisabled();
      expect(screen.getByText('Connecting to chat server...')).toBeInTheDocument();

      // Simulate the connection succeeding
      await act(async () => {
        resolveStart();
        // Wait for the promise chain to resolve
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      // Verify UI updated to connected state
      expect(screen.getByPlaceholderText('Type a message...')).toBeInTheDocument();
      expect(screen.queryByText('Connecting to chat server...')).not.toBeInTheDocument();
    });
  });

  it('stops connection on unmount', () => {
    const { unmount } = render(<ChatComponent />);
    unmount();
    expect(mockConnection.stop).toHaveBeenCalledTimes(1);
  });
});