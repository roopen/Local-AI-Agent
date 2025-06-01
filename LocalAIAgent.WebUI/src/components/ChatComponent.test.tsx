import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import ChatComponent from './ChatComponent';
import * as ChatClient from '../clients/ChatClient';
import { HubConnectionState } from '@microsoft/signalr';

// Mock the ChatClient module
jest.mock('../clients/ChatClient');

const mockOnMessageReceived = jest.fn();
const mockSendMessage = jest.fn();
const mockGetConnection = jest.fn();

describe('ChatComponent', () => {
  beforeEach(() => {
    // Reset mocks before each test
    jest.clearAllMocks();

    // Mock the ChatClient functions
    (ChatClient.onMessageReceived as jest.Mock).mockReturnValue(mockOnMessageReceived);
    (ChatClient.sendMessage as jest.Mock).mockImplementation(mockSendMessage);
    (ChatClient.getConnection as jest.Mock).mockImplementation(mockGetConnection);

    // Mock the connection object and its state
    const mockConnection = {
      state: HubConnectionState.Connected,
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      on: jest.fn(),
      off: jest.fn(),
      invoke: jest.fn(),
    };
    mockGetConnection.mockReturnValue(mockConnection);
  });

  it('renders the chat component', () => {
    render(<ChatComponent />);
    expect(screen.getByPlaceholderText('Type a message...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send/i })).toBeInTheDocument();
    expect(screen.getByText('No messages yet.')).toBeInTheDocument();
  });

  it('sends a message when the form is submitted', async () => {
    render(<ChatComponent />);

    const inputElement = screen.getByPlaceholderText('Type a message...') as HTMLInputElement;
    const sendButton = screen.getByRole('button', { name: /send/i });

    fireEvent.change(inputElement, { target: { value: 'Test message' } });
    fireEvent.click(sendButton);

    await waitFor(() => {
      expect(mockSendMessage).toHaveBeenCalledWith('You', 'Test message');
    });

    expect(inputElement.value).toBe(''); // Input should be cleared after sending
  });

  it('displays received messages', async () => {
    render(<ChatComponent />);

    // Simulate receiving a message
    const receivedMessage = { user: 'Bot', message: 'Hello from bot', id: 'msg2' };
    // Find the mock callback function passed to onMessageReceived
    const messageHandler = (ChatClient.onMessageReceived as jest.Mock).mock.calls[0][0];
    messageHandler(receivedMessage);

    await waitFor(() => {
      expect(screen.getByText('Bot: Hello from bot')).toBeInTheDocument();
    });
  });

  it('displays connecting status when not connected', () => {
    // Mock the connection state to be Disconnected
    const mockConnection = {
      state: HubConnectionState.Disconnected,
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      on: jest.fn(),
      off: jest.fn(),
      invoke: jest.fn(),
    };
    (ChatClient.getConnection as jest.Mock).mockReturnValue(mockConnection);

    render(<ChatComponent />);

    expect(screen.getByPlaceholderText('Connecting...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled();
    expect(screen.getByText('Connecting to chat server...')).toBeInTheDocument();
  });

  it('attempts to start connection on mount if disconnected', () => {
    const mockConnection = {
      state: HubConnectionState.Disconnected,
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      on: jest.fn(),
      off: jest.fn(),
      invoke: jest.fn(),
    };
    (ChatClient.getConnection as jest.Mock).mockReturnValue(mockConnection);

    render(<ChatComponent />);

    expect(mockConnection.start).toHaveBeenCalled();
  });

  it('stops connection on unmount', () => {
    const mockConnection = {
      state: HubConnectionState.Connected,
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      on: jest.fn(),
      off: jest.fn(),
      invoke: jest.fn(),
    };
    (ChatClient.getConnection as jest.Mock).mockReturnValue(mockConnection);

    const { unmount } = render(<ChatComponent />);
    unmount();

    expect(mockConnection.stop).toHaveBeenCalled();
  });
});