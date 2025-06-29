// Tests for the UserSettingsService.

import { userSettingsService } from './UserSettingsService';
import UserSettings from '../domain/UserSettings';

describe('UserSettingsService', () => {
    beforeEach(() => {
        localStorage.clear();
    });

    it('should return default settings when none are saved', () => {
        const settings = userSettingsService.getSettings();
        expect(settings).toEqual(new UserSettings());
        expect(settings.isEmpty()).toBe(true);
    });

    it('should save and retrieve settings correctly', () => {
        const newSettings = new UserSettings(['coding', 'testing'], ['bugs']);
        
        userSettingsService.saveSettings(newSettings);
        const retrievedSettings = userSettingsService.getSettings();

        expect(retrievedSettings).toEqual(newSettings);
        expect(retrievedSettings.getSummary()).toBe('Likes: coding, testing | Dislikes: bugs | System Prompt: ');
    });

    it('should overwrite existing settings', () => {
        const initialSettings = new UserSettings(['reading'], []);
        userSettingsService.saveSettings(initialSettings);

        const newSettings = new UserSettings(['gaming'], ['meetings']);
        userSettingsService.saveSettings(newSettings);

        const retrievedSettings = userSettingsService.getSettings();
        expect(retrievedSettings).toEqual(newSettings);
    });

    it('should check if settings exist', () => {
        expect(userSettingsService.settingsExist()).toBe(false);
        const settings = new UserSettings(['music'], ['noise']);
        userSettingsService.saveSettings(settings);
        expect(userSettingsService.settingsExist()).toBe(true);
    });
});