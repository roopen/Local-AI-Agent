class ChatMessage {
    user: string;
    message: string;
    id: string;

    constructor(user: string, message: string, id: string) {
        this.user = user;
        this.message = message;
        this.id = id;
    }

    toString(): string {
        return `${this.user}: ${this.message}`;
    }

    isEmpty(): boolean {
        return !this.message || this.message.trim() === '';
    }

    tryAppend(message: ChatMessage): boolean {
        if (message.isEmpty()) return false;
        if (this.user !== message.user) return false;
        if (this.id === message.id) return true; //message already appended

        this.message += `${message.message}`;
        this.id = message.id;
        return true;
    }
}

export default ChatMessage;