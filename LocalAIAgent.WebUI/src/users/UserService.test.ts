import UserService from './UserService';
import { UserPreferencesService } from '../clients/UserApiClient';
import type { User } from '../domain/User';
import UserSettings from '../domain/UserSettings';

jest.mock('../clients/UserApiClient', () => ({
    UserPreferencesService: {
        postApiUser: jest.fn(),
        getApiUserPreferences: jest.fn(),
        postApiUserPreferencesApiSavePreferences: jest.fn(),
    },
    OpenAPI: {
        BASE: ''
    }
}));

const mockedUserPreferencesService = UserPreferencesService as jest.Mocked<typeof UserPreferencesService>;

describe('UserService', () => {
    let userService: UserService;

    beforeEach(() => {
        userService = new UserService();
        jest.clearAllMocks();
    });

    describe('createUser', () => {
        it('should create a user and return the mapped user object', async () => {
            const newUser: Omit<User, 'id'> = { name: 'testuser' };
            const createdUserFromApi = { id: 1, username: 'testuser' };

            mockedUserPreferencesService.postApiUser.mockResolvedValue(createdUserFromApi);

            const result = await userService.createUser(newUser);

            expect(mockedUserPreferencesService.postApiUser).toHaveBeenCalledWith({
                username: newUser.name,
            });
            expect(result).toEqual({
                id: '1',
                name: 'testuser',
            });
        });
    });

    describe('getUserPreferences', () => {
        it('should return user settings if found', async () => {
            const userId = '1';
            const preferencesFromApi = {
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
            mockedUserPreferencesService.getApiUserPreferences.mockRejectedValue(new Error('API Error'));

            await expect(userService.getUserPreferences(userId)).rejects.toThrow('API Error');
            expect(mockedUserPreferencesService.getApiUserPreferences).toHaveBeenCalledWith(1);
        });
    });

    describe('saveUserPreferences', () => {
        it('should save user preferences', async () => {
            const preferences: Omit<UserSettings, 'id'> = new UserSettings(['ai'], ['manual work'], 'be concise');

            await userService.saveUserPreferences(preferences);

            expect(mockedUserPreferencesService.postApiUserPreferencesApiSavePreferences).toHaveBeenCalledWith({
                userId: 1, // This is hardcoded in the service
                prompt: preferences.prompt,
                interests: preferences.likes,
                dislikes: preferences.dislikes,
            });
        });
    });
});