import type { User } from "./../domain/User";
import type UserSettings from "./../domain/UserSettings";

export interface IUserService {
    createUser(user: Omit<User, "id">): Promise<User>;
    getUserPreferences(userId: string): Promise<UserSettings | null>;
    saveUserPreferences(preferences: Omit<UserSettings, "id">): Promise<void>;
}