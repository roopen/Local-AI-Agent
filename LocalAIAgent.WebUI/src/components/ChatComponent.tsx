import React, { useState, type FormEvent } from 'react';

function ChatComponent() {
  const [messages, setMessages] = useState<string[]>([]);
  const [input, setInput] = useState<string>('');

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (input.trim() === '') return;
    setMessages([...messages, input]);
    setInput('');
  };

  return (
    <div style={{ maxWidth: 400, margin: '0 auto' }}>
      <div style={{ border: '1px solid #ccc', padding: 10, minHeight: 200, marginBottom: 10 }}>
        {messages.length === 0 ? (
          <p>No messages yet.</p>
        ) : (
          messages.map((msg, idx) => (
            <div key={idx} style={{ margin: '5px 0' }}>
              {msg}
            </div>
          ))
        )}
      </div>
      <form onSubmit={handleSubmit} style={{ display: 'flex' }}>
        <input
          type="text"
          value={input}
          onChange={e => setInput(e.target.value)}
          style={{ flex: 1, marginRight: 5 }}
          placeholder="Type a message..."
        />
        <button type="submit">Send</button>
      </form>
    </div>
  );
}

export default ChatComponent;