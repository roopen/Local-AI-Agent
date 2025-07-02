import ChatMessage from './ChatMessage';

describe('ChatMessage', () => {
  it('should create a ChatMessage instance correctly', () => {
    const message = new ChatMessage('user1', 'Hello', 'msg1');
    expect(message.user).toBe('user1');
    expect(message.message).toBe('Hello');
    expect(message.id).toBe('msg1');
  });

  it('should return the correct string representation', () => {
    const message = new ChatMessage('user1', 'Hello', 'msg1');
    expect(message.toString()).toBe('user1: Hello');
  });

  it('should return true for an empty message', () => {
    const message1 = new ChatMessage('user1', '', 'msg1');
    const message2 = new ChatMessage('user1', '   ', 'msg2');
    expect(message1.isEmpty()).toBe(true);
    expect(message2.isEmpty()).toBe(true);
  });

  it('should return false for a non-empty message', () => {
    const message = new ChatMessage('user1', 'Hello', 'msg1');
    expect(message.isEmpty()).toBe(false);
  });

  it('should append message if user is the same and message is not empty', () => {
    const message1 = new ChatMessage('user1', 'Hello', 'msg1');
    const message2 = new ChatMessage('user1', ' World', 'msg2');
    const result = message1.tryAppend(message2);
    expect(result).toBe(true);
    expect(message1.message).toBe('Hello World');
    expect(message1.id).toBe('msg2');
  });

  it('should not append message if user is different', () => {
    const message1 = new ChatMessage('user1', 'Hello', 'msg1');
    const message2 = new ChatMessage('user2', ' World', 'msg2');
    const result = message1.tryAppend(message2);
    expect(result).toBe(false);
    expect(message1.message).toBe('Hello');
    expect(message1.id).toBe('msg1');
  });

  it('should not append message if the message to append is empty', () => {
    const message1 = new ChatMessage('user1', 'Hello', 'msg1');
    const message2 = new ChatMessage('user1', '', 'msg2');
    const result = message1.tryAppend(message2);
    expect(result).toBe(false);
    expect(message1.message).toBe('Hello');
    expect(message1.id).toBe('msg1');
  });

  it('should return true and not append if the message id is the same', () => {
    const message1 = new ChatMessage('user1', 'Hello', 'msg1');
    const message2 = new ChatMessage('user1', ' World', 'msg1');
    const result = message1.tryAppend(message2);
    expect(result).toBe(true);
    expect(message1.message).toBe('Hello');
    expect(message1.id).toBe('msg1');
  });
});