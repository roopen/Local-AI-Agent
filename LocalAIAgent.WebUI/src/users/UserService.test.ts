import UserService from './UserService';
import { LoginService, UserPreferencesService } from '../clients/UserApiClient';
import UserSettings from '../domain/UserSettings';
import type { UserRegistrationDto, UserLoginDto, UserDto, UserPreferenceDto } from '../clients/UserApiClient';

jest.mock('../clients/UserApiClient', () => ({
    LoginService: {
        postApiLoginLogin: jest.fn(),
        postApiLoginRegister: jest.fn(),
    },
    UserPreferencesService: {
        getApiUserPreferences: jest.fn(),
        postApiSavePreferences: jest.fn(),
    },
    OpenAPI: {
        BASE: ''
    }
}));

const mockedLoginService = LoginService as jest.Mocked<typeof LoginService>;
const mockedUserPreferencesService = UserPreferencesService as jest.Mocked<typeof UserPreferencesService>;

describe('UserService', () => {
    let userService: UserService;

    beforeEach(() => {
        userService = new UserService();
        jest.clearAllMocks();
    });

    describe('login', () => {
        it('should log in a user and return the mapped user object', async () => {
            const userCredentials: UserLoginDto = { username: 'testuser', password: 'password' };
            const loggedInUserFromApi = { id: 1, username: 'testuser' };

            mockedLoginService.postApiLoginLogin.mockResolvedValue(loggedInUserFromApi);

            const result = await userService.login(userCredentials);

            expect(mockedLoginService.postApiLoginLogin).toHaveBeenCalledWith(userCredentials);
            expect(result).toEqual({
                id: '1',
                name: 'testuser',
            });
            expect(userService.isLoggedIn()).toBe(true);
            expect(userService.getCurrentUser()).toEqual({
                id: '1',
                name: 'testuser',
            });
        });

        it('should return null if login fails', async () => {
            const userCredentials: UserLoginDto = { username: 'testuser', password: 'password' };
            mockedLoginService.postApiLoginLogin.mockResolvedValue(null as unknown as UserDto);

            const result = await userService.login(userCredentials);

            expect(result).toBeNull();
            expect(userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
        });
    });

    describe('register', () => {
        it('should register a user and return the mapped user object', async () => {
            const newUser: UserRegistrationDto = { username: 'testuser', password: 'password' };
            const registeredUserFromApi = { id: 1, username: 'testuser' };

            mockedLoginService.postApiLoginRegister.mockResolvedValue(registeredUserFromApi);

            const result = await userService.register(newUser);

            expect(mockedLoginService.postApiLoginRegister).toHaveBeenCalledWith(newUser);
            expect(result).toEqual({
                id: '1',
                name: 'testuser',
            });
            expect(userService.isLoggedIn()).toBe(true);
            expect(userService.getCurrentUser()).toEqual({
                id: '1',
                name: 'testuser',
            });
        });

        it('should return null if registration fails', async () => {
            const newUser: UserRegistrationDto = { username: 'testuser', password: 'password' };
            mockedLoginService.postApiLoginRegister.mockResolvedValue(null as unknown as UserDto);

            const result = await userService.register(newUser);

            expect(result).toBeNull();
            expect(userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
        });
    });

    describe('logout', () => {
        it('should log out the user', async () => {
            const userCredentials: UserLoginDto = { username: 'testuser', password: 'password' };
            const loggedInUserFromApi = { id: 1, username: 'testuser' };
            mockedLoginService.postApiLoginLogin.mockResolvedValue(loggedInUserFromApi);
            await userService.login(userCredentials);

            expect(userService.isLoggedIn()).toBe(true);

            userService.logout();

            expect(userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
        });
    });

    describe('isLoggedIn and getCurrentUser', () => {
        it('should return false and null when no user is logged in', () => {
            expect(userService.isLoggedIn()).toBe(false);
            expect(userService.getCurrentUser()).toBeNull();
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
            mockedUserPreferencesService.getApiUserPreferences.mockResolvedValue(null as unknown as UserPreferenceDto);

            const result = await userService.getUserPreferences(userId);
            expect(result).toBeNull();
        });
    });

    describe('saveUserPreferences', () => {
        it('should throw an error if user is not logged in', async () => {
            const preferences = new UserSettings(['ai'], ['manual work'], 'be concise');
            await expect(userService.saveUserPreferences(preferences)).rejects.toThrow("User not logged in");
        });

        it('should save user preferences when user is logged in', async () => {
            const userCredentials: UserLoginDto = { username: 'testuser', password: 'password' };
            const loggedInUserFromApi = { id: 1, username: 'testuser' };
            mockedLoginService.postApiLoginLogin.mockResolvedValue(loggedInUserFromApi);
            await userService.login(userCredentials);

            const preferences = new UserSettings(['ai'], ['manual work'], 'be concise');
            await userService.saveUserPreferences(preferences);

            expect(mockedUserPreferencesService.postApiSavePreferences).toHaveBeenCalledWith({
                userId: 1,
                prompt: preferences.prompt,
                interests: preferences.likes,
                dislikes: preferences.dislikes,
            });
        });
    });
});