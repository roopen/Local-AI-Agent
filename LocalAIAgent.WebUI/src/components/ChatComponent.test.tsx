import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import '@testing-library/jest-dom';
import ChatComponent from './ChatComponent';
import { ChatConnection } from '../clients/ChatClient';
import { HubConnectionState } from '@microsoft/signalr';
import NewsArticle from '../domain/NewsArticle';
import { NewsClient } from '../clients/NewsClient';

// Mock the ChatConnection class
jest.mock('../clients/ChatClient');

const mockSingleton = {
  getSummary: jest.fn().mockResolvedValue('mock summary'),
  getRelatedArticles: jest.fn().mockResolvedValue([]),
  getArticleById: jest.fn().mockResolvedValue(null),
  getExpandedNews: jest.fn().mockResolvedValue(null),
} as unknown as NewsClient;

jest.spyOn(NewsClient, 'getInstance').mockReturnValue(mockSingleton);

// Mock react-markdown and remark-gfm
jest.mock('react-markdown', () => (props: React.PropsWithChildren<object>) => {
  return <div>{props.children}</div>;
});
jest.mock('remark-gfm', () => () => {});

const mockedChatConnection = ChatConnection as jest.MockedClass<typeof ChatConnection>;

const newsArticle = new NewsArticle(
  'Sample News Title',
  'Sample news summary for testing.',
  new Date(),
  'https://example.com/news/sample-news-title',
  'Example News',
  ['Technology', 'AI'],
  'High',
  'US',
);

describe('ChatComponent', () => {
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
      onMessageReceived: jest.fn().mockImplementation(() => {
        return jest.fn();
      }),
      sendMessage: jest.fn().mockResolvedValue(undefined),
      getState: jest.fn().mockReturnValue(HubConnectionState.Connected),
    };

    mockedChatConnection.mockImplementation(() => mockConnectionInstance as unknown as ChatConnection);
  });

  it('renders the chat component when connected', async () => {
    await act(async () => {
      render(<ChatComponent article={newsArticle} />);
    });
    expect(screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /send/i })).toBeInTheDocument();
    expect(screen.getByText('Ask away.')).toBeInTheDocument();
  });

  it('sends a message when the form is submitted', async () => {
    await act(async () => {
      render(<ChatComponent article={newsArticle} />);
    });

    const inputElement = screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)');
    const sendButton = screen.getByRole('button', { name: /send/i });

    await act(async () => {
      fireEvent.change(inputElement, { target: { value: 'Test message' } });
    });

    await act(async () => {
      fireEvent.click(sendButton);
    });

    await waitFor(() => {
      expect(mockConnectionInstance.sendMessage).toHaveBeenCalledWith('You', 'Test message');
    });

    expect((inputElement as HTMLInputElement).value).toBe('');
  });

  describe('when connection is not established', () => {
    it('displays connecting status and connects successfully', async () => {
      let resolveStart: () => void;
      const startPromise = new Promise<void>((resolve) => {
        resolveStart = resolve;
      });

      mockConnectionInstance.getState.mockReturnValue(HubConnectionState.Disconnected);
      mockConnectionInstance.start.mockReturnValue(startPromise);

      await act(async () => {
        render(<ChatComponent article={newsArticle} />);
      });

      expect(mockConnectionInstance.start).toHaveBeenCalledTimes(1);
      expect(screen.getByPlaceholderText('Connecting...')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /send/i })).toBeDisabled();
      expect(screen.getByText('Connecting to chat server...')).toBeInTheDocument();

      mockConnectionInstance.getState.mockReturnValue(HubConnectionState.Connected);

      await act(async () => {
        resolveStart();
        await new Promise((resolve) => setTimeout(resolve, 0));
      });

      expect(screen.getByPlaceholderText('Type a message... (Shift+Enter for new line)')).toBeInTheDocument();
      expect(screen.queryByText('Connecting to chat server...')).not.toBeInTheDocument();
    });
  });

  it('stops connection on unmount', async () => {
    let unmountFn: () => void;

    await act(async () => {
      const { unmount } = render(<ChatComponent article={newsArticle} />);
      unmountFn = unmount;
    });

    await act(async () => {
      unmountFn();
    });

    expect(mockConnectionInstance.stop).toHaveBeenCalledTimes(1);
  });
});