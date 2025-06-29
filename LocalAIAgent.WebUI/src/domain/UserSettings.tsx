class UserSettings {
    likes: string[];
    dislikes: string[];
    prompt: string;

    constructor(likes: string[] = [], dislikes: string[] = [], prompt: string = '') {
        this.likes = likes.filter(this.isValid);
        this.dislikes = dislikes.filter(this.isValid);
        this.prompt = prompt;
    }

    addLike(item: string): void {
        if (this.isValid(item) && !this.likes.includes(item)) {
            this.likes.push(item);
        }
    }

    addDislike(item: string): void {
        if (this.isValid(item) && !this.dislikes.includes(item)) {
            this.dislikes.push(item);
        }
    }

    removeLike(item: string): void {
        this.likes = this.likes.filter(like => like !== item);
    }

    removeDislike(item: string): void {
        this.dislikes = this.dislikes.filter(dislike => dislike !== item);
    }

    getSummary(): string {
        return `Likes: ${this.likes.join(', ')} | Dislikes: ${this.dislikes.join(', ')} | System Prompt: ${this.prompt}`;
    }

    isEmpty(): boolean {
        return this.likes.length === 0 && this.dislikes.length === 0 && (!this.prompt || this.prompt.trim().length === 0);
    }

    static fromJSON(json: string): UserSettings {
        try {
            const data = JSON.parse(json);
            return new UserSettings(data.likes || [], data.dislikes || [], data.prompt || '');
        } catch (error) {
            console.error('Invalid JSON format:', error);
            return new UserSettings();
        }
    }

    toJSON(): string {
        return JSON.stringify({
            likes: this.likes,
            dislikes: this.dislikes,
            prompt: this.prompt
        });
    }

    private isValid(value: string): value is string {
        return typeof value === 'string' && value.trim().length > 0;
    }
}

export default UserSettings;
