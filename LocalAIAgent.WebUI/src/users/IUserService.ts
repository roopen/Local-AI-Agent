import type { AiSettingsCatalogResponse, AiSettingsOptionResponse, CredentialInfo, RegistrationStatusDto, SaveAiSettingsOptionRequest } from "../clients/UserApiClient";
import type AISettings from "../domain/AISettings";
import type { User } from "../domain/User";
import type UserSettings from "../domain/UserSettings";

export interface IUserService {
    login(): Promise<User | null>;
    register(username: string, inviteToken?: string): Promise<User | null>;
    getRegistrationStatus(): Promise<RegistrationStatusDto>;
    logout(): Promise<void>;
    getCurrentUser(): User | null;
    isLoggedIn(): Promise<boolean>;
    getUserPreferences(): Promise<UserSettings | null>;
    saveUserPreferences(preferences: Omit<UserSettings, "id">): Promise<void>;
    getCredentials(): Promise<CredentialInfo[]>;
    removeCredential(id: string): Promise<void>;
    addCredential(): Promise<void>;
    getAiSettings(): Promise<AISettings>;
    saveAiSettings(settings: AISettings, clearApiKey?: boolean): Promise<AISettings>;
    getLlmOptions(): Promise<AiSettingsCatalogResponse>;
    saveLlmOption(option: SaveAiSettingsOptionRequest, settingsId?: number): Promise<AiSettingsOptionResponse>;
    deleteLlmOption(settingsId: number): Promise<void>;
    selectLlmOption(settingsId: number): Promise<void>;
}
