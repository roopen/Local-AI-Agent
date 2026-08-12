import UserService from './UserService';
import { 
    LoginService, 
    UserPreferencesService, 
    AiSettingsService,
    Fido2Service,
    UserVerificationRequirement,
    AttestationConveyancePreference
} from '../clients/UserApiClient';
import UserSettings from '../domain/UserSettings';
import AISettings from '../domain/AISettings';
import type { 
    AssertionOptions,
    CredentialCreateOptions,
    UserPreferenceDto
} from '../clients/UserApiClient';
import type { UserDto } from '../clients/UserApiClient';

jest.mock('../clients/UserApiClient', () => ({
    LoginService: {
        postApiLoginLogin: jest.fn(),
        postApiLoginRegister: jest.fn(),
        postApiLoginLogout: jest.fn(),
        getApiLoginCurrent: jest.fn(),
    },
    UserPreferencesService: {
        getApiUserPreferences: jest.fn(),
        postApiSavePreferences: jest.fn(),
        postApiSaveAiSettings: jest.fn(),
        getApiAiSettings: jest.fn(),
    },
    AiSettingsService: {
        getApiAiSettings: jest.fn(),
        putApiAiSettings: jest.fn(),
    },
    Fido2Service: {
        postAssertionOptions: jest.fn(),
        postMakeAssertion: jest.fn(),
        postMakeCredentialOptions: jest.fn(),
        postMakeCredential: jest.fn(),
    },
    OpenAPI: {
        BASE: ''
    },
    PublicKeyCredentialType: {
        PUBLIC_KEY: 'public-key'
    },
    UserVerificationRequirement: {
        PREFERRED: 'preferred'
    },
    AttestationConveyancePreference: {
        NONE: 'none'
    }
}));

const mockedLoginService = LoginService as jest.Mocked<typeof LoginService>;
const mockedUserPreferencesService = UserPreferencesService as jest.Mocked<typeof UserPreferencesService>;
const mockedAiSettingsService = AiSettingsService as jest.Mocked<typeof AiSettingsService>;
const mockedFido2Service = Fido2Service as jest.Mocked<typeof Fido2Service>;

const mockNavigatorCredentials = {
    create: jest.fn(),
    get: jest.fn(),
};

Object.defineProperty(global, 'navigator', {
    writable: true,
    value: {
        credentials: mockNavigatorCredentials,
    },
});

describe('UserService', () => {
    let userService: UserService;

    beforeEach(() => {
        jest.clearAllMocks();
        mockNavigatorCredentials.create.mockReset();
        mockNavigatorCredentials.get.mockReset();
        userService = UserService.getInstance();
    });

    afterEach(async () => {
        await userService.logout();
    });

    describe('login', () => {
        it('should perform Fido2 login sequence', async () => {
            const assertionOptions: AssertionOptions = {
                challenge: 'AAAA',
                timeout: 60000,
                rpId: 'localhost',
                allowCredentials: [],
                userVerification: UserVerificationRequirement.PREFERRED
            };
            mockedFido2Service.postAssertionOptions.mockResolvedValue(assertionOptions);

            const credentialMock = {
                id: 'AAAA',
                rawId: new Uint8Array([1, 2, 3]).buffer,
                type: 'public-key',
                response: {
                    authenticatorData: new Uint8Array([4, 5, 6]).buffer,
                    clientDataJSON: new Uint8Array([7, 8, 9]).buffer,
                    signature: new Uint8Array([10, 11, 12]).buffer,
                },
                getClientExtensionResults: jest.fn().mockReturnValue({})
            };
            mockNavigatorCredentials.get.mockResolvedValue(credentialMock);

            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            mockedFido2Service.postMakeAssertion.mockResolvedValue({ status: 'ok', errorMessage: '' } as any);

            const result = await userService.login();

            expect(mockedFido2Service.postAssertionOptions).toHaveBeenCalled();
            expect(mockNavigatorCredentials.get).toHaveBeenCalled();
            expect(mockedFido2Service.postMakeAssertion).toHaveBeenCalledWith(expect.objectContaining({
                id: 'AAAA'
            }));
            expect(result).toBeNull();
        });
    });

    describe('register', () => {
        it('should perform Fido2 registration sequence', async () => {
            const username = 'testuser';
            const credentialCreateOptions: CredentialCreateOptions = {
                challenge: 'AAAA',
                rp: { name: 'rp', id: 'rp-id' },
                user: { id: 'AAAA', name: 'user', displayName: 'User' },
                pubKeyCredParams: [],
                timeout: 60000,
                attestation: AttestationConveyancePreference.NONE
            };
            mockedFido2Service.postMakeCredentialOptions.mockResolvedValue(credentialCreateOptions);

            const credentialMock = {
                id: 'AAAA',
                rawId: new Uint8Array([13, 14, 15]).buffer,
                type: 'public-key',
                response: {
                    clientDataJSON: new Uint8Array([16, 17, 18]).buffer,
                    attestationObject: new Uint8Array([19, 20, 21]).buffer,
                    getTransports: jest.fn().mockReturnValue([])
                },
                getClientExtensionResults: jest.fn().mockReturnValue({})
            };
            mockNavigatorCredentials.create.mockResolvedValue(credentialMock);

            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            mockedFido2Service.postMakeCredential.mockResolvedValue({ id: 1, user: { id: 1, name: 'testuser' } } as any);

            const result = await userService.register(username);

            expect(mockedFido2Service.postMakeCredentialOptions).toHaveBeenCalledWith(username);
            expect(mockNavigatorCredentials.create).toHaveBeenCalled();
            expect(mockedFido2Service.postMakeCredential).toHaveBeenCalledWith(expect.objectContaining({
                attestation: expect.objectContaining({
                    id: 'AAAA'
                })
            }));
            expect(result).toBeNull();
        });
    });

    describe('logout', () => {
        it('should log out the user', async () => {
            mockedLoginService.postApiLoginLogout.mockResolvedValue(undefined);
            
            await userService.logout();

            expect(mockedLoginService.postApiLoginLogout).toHaveBeenCalled();
            expect(userService.getCurrentUser()).toBeNull();
        });
    });

    describe('isLoggedIn and getCurrentUser', () => {
        it('should return false and null when no user is logged in', async () => {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            mockedLoginService.getApiLoginCurrent.mockResolvedValue(null as any);
            expect(await userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
        });

        it('should return true and the user when a user is logged in', async () => {
            const loggedInUserFromApi: UserDto = { id: 1, username: 'testuser' };
            mockedLoginService.getApiLoginCurrent.mockResolvedValue(loggedInUserFromApi);
            expect(await userService.isLoggedIn()).toBe(true);
            expect(userService.getCurrentUser()).toEqual({
                id: '1',
                name: 'testuser',
            });
        });

        it('should return false when the api call fails', async () => {
            mockedLoginService.getApiLoginCurrent.mockRejectedValue(new Error('Network error'));
            expect(await userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
        });
    });

    describe('getUserPreferences', () => {
        it('should return user settings if found', async () => {
            const userId = '1';
            const preferencesFromApi: UserPreferenceDto = {
                interests: ['coding', 'testing'],
                dislikes: ['bugs'],
                prompt: 'act as a senior developer',
            };

            mockedUserPreferencesService.getApiUserPreferences.mockResolvedValue(preferencesFromApi);

            const result = await userService.getUserPreferences(userId);

            expect(mockedUserPreferencesService.getApiUserPreferences).toHaveBeenCalledWith(1);
            expect(result).toBeInstanceOf(UserSettings);
            expect(result?.likes).toStrictEqual(preferencesFromApi.interests);
            expect(result?.dislikes).toStrictEqual(preferencesFromApi.dislikes);
            expect(result?.prompt).toBe(preferencesFromApi.prompt);
        });

        it('should return null if user preferences are not found', async () => {
            const userId = '1';
            mockedUserPreferencesService.getApiUserPreferences.mockResolvedValue(null as unknown as UserPreferenceDto);

            const result = await userService.getUserPreferences(userId);
            expect(result).toBeNull();
        });
    });

    describe('saveUserPreferences', () => {
        it('should throw an error if user is not logged in', async () => {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            mockedLoginService.getApiLoginCurrent.mockResolvedValue(null as any);
            await userService.isLoggedIn();

            const preferences = new UserSettings(['ai'], ['manual work'], 'be concise');
            await expect(userService.saveUserPreferences(preferences)).rejects.toThrow("User not logged in");
        });

        it('should save user preferences when user is logged in', async () => {
            const loggedInUserFromApi: UserDto = { id: 1, username: 'testuser' };
            mockedLoginService.getApiLoginCurrent.mockResolvedValue(loggedInUserFromApi);
            await userService.isLoggedIn();

            const preferences = new UserSettings(['ai'], ['manual work'], 'be concise');
            await userService.saveUserPreferences(preferences);

            expect(mockedUserPreferencesService.postApiSavePreferences).toHaveBeenCalledWith({
                userId: 1,
                prompt: preferences.prompt,
                interests: preferences.likes,
                dislikes: preferences.dislikes,
                targetLanguage: preferences.targetLanguage,
                disabledFeedSources: preferences.disabledFeedSources,
            });
        });
    });

    describe('AI settings', () => {
        it('loads current-user status without expecting a token value', async () => {
            mockedLoginService.getApiLoginCurrent.mockResolvedValue({ id: 1, username: 'testuser' });
            await userService.isLoggedIn();
            mockedAiSettingsService.getApiAiSettings.mockResolvedValue({
                isConfigured: true,
                hasApiKey: true,
                modelId: 'unsloth-model',
                endpointUrl: 'http://localhost:8000/v1/',
            });

            const result = await userService.getAiSettings();

            expect(mockedAiSettingsService.getApiAiSettings).toHaveBeenCalledWith();
            expect(result.isConfigured).toBe(true);
            expect(result.hasApiKey).toBe(true);
            expect(result.apiKey).toBe('');
        });

        it('submits a blank token as null so an existing token is preserved', async () => {
            mockedLoginService.getApiLoginCurrent.mockResolvedValue({ id: 1, username: 'testuser' });
            await userService.isLoggedIn();
            mockedAiSettingsService.putApiAiSettings.mockResolvedValue({
                isConfigured: true,
                hasApiKey: true,
                modelId: 'model',
                endpointUrl: 'http://localhost:1234/v1/',
            });

            await userService.saveAiSettings(new AISettings(
                false,
                true,
                'model',
                'http://localhost:1234/v1/'));

            expect(mockedAiSettingsService.putApiAiSettings).toHaveBeenCalledWith(expect.objectContaining({
                modelId: 'model',
                apiKey: null,
                clearApiKey: false,
            }));
        });
    });
});
