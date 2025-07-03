import type { User } from "../domain/User";
import type UserSettings from "../domain/UserSettings";
import type { UserLoginDto, UserRegistrationDto } from "../clients/UserApiClient";

export interface IUserService {
    login(user: UserLoginDto): Promise<User | null>;
    register(user: UserRegistrationDto): Promise<User | null>;
    logout(): void;
    getCurrentUser(): User | null;
    isLoggedIn(): Promise<boolean>;
    getUserPreferences(userId: string): Promise<UserSettings | null>;
    saveUserPreferences(preferences: Omit<UserSettings, "id">): Promise<void>;
}