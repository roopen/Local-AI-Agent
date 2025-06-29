import UserSettings from "../domain/UserSettings";

/**
 * Service for managing user settings.
 */
class UserSettingsService {
    private readonly storageKey = 'userSettings';

    /**
     * Gets the user settings from local storage.
     * @returns The user settings.
     */
    public getSettings(): UserSettings {
        const settings = localStorage.getItem(this.storageKey);
        if (settings) {
            return UserSettings.fromJSON(settings);
        }

        return new UserSettings();
    }

    /**
     * Saves the user settings to local storage.
     * @param settings The user settings to save.
     */
    public saveSettings(settings: UserSettings): void {
        localStorage.setItem(this.storageKey, settings.toJSON());
    }

    public settingsExist(): boolean {
        const settings = localStorage.getItem(this.storageKey);
        return settings !== null && settings.trim() !== '';
    }
}

export const userSettingsService = new UserSettingsService();