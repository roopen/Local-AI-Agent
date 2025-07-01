import { OpenAPI, UserPreferencesService } from "../clients/UserApiClient";
import type { User } from "../domain/User";
import type { IUserService } from "./IUserService";
import UserSettings from "../domain/UserSettings";

OpenAPI.BASE = "https://localhost:7276";
const userId = 1;

export default class UserService implements IUserService {
    async createUser(user: Omit<User, "id">): Promise<User> {
        const createdUser = await UserPreferencesService.postApiUser({
            username: user.name
        });
        return {
            id: createdUser.id!.toString(),
            name: createdUser.username!
        };
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
        await UserPreferencesService.postApiSavePreferences({
            userId: userId,
            prompt: preferences.prompt,
            interests: preferences.likes,
            dislikes: preferences.dislikes
        });
    }
}