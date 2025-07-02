import { LoginService, OpenAPI, UserPreferencesService } from "../clients/UserApiClient";
import type { UserLoginDto, UserRegistrationDto } from "../clients/UserApiClient";
import type { User } from "../domain/User";
import type { IUserService } from "./IUserService";
import UserSettings from "../domain/UserSettings";

OpenAPI.BASE = "https://localhost:7276";

export default class UserService implements IUserService {
    private static _instance: UserService;
    private _currentUser: User | null = null;

    private constructor() {
    }

    public static getInstance(): UserService {
        if (!UserService._instance) {
            UserService._instance = new UserService();
        }
        return UserService._instance;
    }

    async login(user: UserLoginDto): Promise<User | null> {
        const loggedInUser = await LoginService.postApiLoginLogin(user);
        if (loggedInUser) {
            this._currentUser = {
                id: loggedInUser.id!.toString(),
                name: loggedInUser.username!
            };
            return this._currentUser;
        }
        return null;
    }

    async register(user: UserRegistrationDto): Promise<User | null> {
        const registeredUser = await LoginService.postApiLoginRegister(user);
        if (registeredUser) {
            this._currentUser = {
                id: registeredUser.id!.toString(),
                name: registeredUser.username!
            };
            return this._currentUser;
        }
        return null;
    }

    logout(): void {
        this._currentUser = null;
    }

    getCurrentUser(): User | null {
        return this._currentUser;
    }

    isLoggedIn(): boolean {
        return this._currentUser !== null;
    }

    async getUserPreferences(userId: string): Promise<UserSettings | null> {
        const preferences = await UserPreferencesService.getApiUserPreferences(parseInt(userId, 10));
        if (!preferences) {
            return null;
        }

        const response = new UserSettings(preferences.interests!, preferences.dislikes!, preferences.prompt!);

        return response;
    }

    async saveUserPreferences(preferences: Omit<UserSettings, "id">): Promise<void> {
        if (!this._currentUser) {
            throw new Error("User not logged in");
        }
        await UserPreferencesService.postApiSavePreferences({
            userId: parseInt(this._currentUser.id, 10),
            prompt: preferences.prompt,
            interests: preferences.likes,
            dislikes: preferences.dislikes
        });
    }
}