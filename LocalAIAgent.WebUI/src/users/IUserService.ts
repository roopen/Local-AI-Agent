import type { CredentialInfo } from "../clients/UserApiClient";
import type AISettings from "../domain/AISettings";
import type { User } from "../domain/User";
import type UserSettings from "../domain/UserSettings";

export interface IUserService {
    login(): Promise<User | null>;
    register(username: string): Promise<User | null>;
    logout(): void;
    getCurrentUser(): User | null;
    isLoggedIn(): Promise<boolean>;
    getUserPreferences(userId: string): Promise<UserSettings | null>;
    saveUserPreferences(preferences: Omit<UserSettings, "id">): Promise<void>;
    getCredentials(): Promise<CredentialInfo[]>;
    removeCredential(id: string): Promise<void>;
    addCredential(): Promise<void>;
    getAiSettings(): Promise<AISettings>;
    saveAiSettings(settings: AISettings): Promise<void>;
}