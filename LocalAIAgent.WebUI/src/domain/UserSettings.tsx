class UserSettings {
    likes: string[];
    dislikes: string[];

    constructor(likes: string[] = [], dislikes: string[] = []) {
        this.likes = likes.filter(this.isValid);
        this.dislikes = dislikes.filter(this.isValid);
    }

    private isValid(value: string): value is string {
        return typeof value === 'string' && value.trim().length > 0;
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
        return `Likes: ${this.likes.join(', ')} | Dislikes: ${this.dislikes.join(', ')}`;
    }
}

export default UserSettings;
