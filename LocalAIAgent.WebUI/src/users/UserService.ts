import { LoginService, OpenAPI, UserPreferencesService, Fido2Service, PublicKeyCredentialType, AuthenticatorTransport } from "../clients/UserApiClient";
import type {
    AssertionOptions,
    AuthenticatorAssertionRawResponse,
    CredentialCreateOptions,
    CredentialInfo,
    CredentialRegistrationRequest,
    RegisteredPublicKeyCredential
} from "../clients/UserApiClient";
import type { User } from "../domain/User";
import type { IUserService } from "./IUserService";
import UserSettings from "../domain/UserSettings";

OpenAPI.BASE = "https://apiainews.dev.localhost:7276";
OpenAPI.CREDENTIALS = "include";
OpenAPI.WITH_CREDENTIALS = true;

export default class UserService implements IUserService {
    private static _instance: UserService;
    private _currentUser: User | null = null;

    private constructor() {
    }

    async getCredentials(): Promise<CredentialInfo[]> {
        return await Fido2Service.getListCredentials();
    }
    
    async removeCredential(id: string): Promise<void> {
        await Fido2Service.postRemoveCredential(id);
    }

    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    async addCredential(): Promise<void> {
        if (!this._currentUser) {
            throw new Error("User must be logged in to add a credential.");
        }

        const options: CredentialCreateOptions = await Fido2Service.postMakeCredentialOptionsExistingUser();

        const credential = await this.getCredentialFromUser(options);

        const authAttestationRawResponse = this.getAttestationRawResponse(credential, this._currentUser.name);

        const requestBody: CredentialRegistrationRequest = {
            attestation: authAttestationRawResponse.attestation,
            credentialName: `${this._currentUser.name} additional credential`
        };

        await Fido2Service.postAddCredentialExistingUser(requestBody);
    }

    public static getInstance(): UserService {
        if (!UserService._instance) {
            UserService._instance = new UserService();
        }
        return UserService._instance;
    }

    async login(): Promise<User | null> {
        const options: AssertionOptions = await Fido2Service.postAssertionOptions();

        console.log('assertion options: ', options);

        const publicKey: CredentialRequestOptions = {
            publicKey: {
                challenge: this.coerceToArrayBuffer(options.challenge, "challenge"),
                timeout: options.timeout,
                rpId: options.rpId!,
                allowCredentials: options.allowCredentials?.map<PublicKeyCredentialDescriptor>(cred => ({
                    type: "public-key",
                    id: this.coerceToArrayBuffer(cred.id, "allowCredentials.id"),
                })),
                userVerification: options.userVerification,
            },
            mediation: "required"
        };

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const credential: any = await navigator.credentials.get(publicKey);

        console.log('credential: ', credential);
        if (!credential) throw new Error("No credential returned from navigator.credentials.get");

        const authData = new Uint8Array(credential.response.authenticatorData);
        const clientDataJSON = new Uint8Array(credential.response.clientDataJSON);
        const rawId = new Uint8Array(credential.rawId);
        const sig = new Uint8Array(credential.response.signature);
        const data: AuthenticatorAssertionRawResponse = {
            id: credential.id,
            rawId: this.coerceToBase64Url(rawId) as string,
            type: credential.type,
            extensions: credential.getClientExtensionResults(),
            response: {
                authenticatorData: this.coerceToBase64Url(authData) as string,
                clientDataJSON: this.coerceToBase64Url(clientDataJSON) as string,
                signature: this.coerceToBase64Url(sig) as string
            },
            clientExtensionResults: UserService.mapClientExtensionResults(credential.getClientExtensionResults())
        };

        const assertion = await Fido2Service.postMakeAssertion(data);

        console.log('assertion result: ', assertion);

        return null;
    }

    async register(username: string): Promise<User | null> {
        if (!username) throw new Error("Username is required");

        const options: CredentialCreateOptions = await Fido2Service.postMakeCredentialOptions(username);

        const credential = await this.getCredentialFromUser(options);

        const authAttestationRawResponse = this.getAttestationRawResponse(credential, username);

        const registrationResult: RegisteredPublicKeyCredential = await Fido2Service.postMakeCredential(authAttestationRawResponse);

        if (registrationResult.user) {
            console.log('registration successful for user: ', registrationResult.user);
        }

        return null;
    }

    async logout(): Promise<void> {
        await LoginService.postApiLoginLogout();
        this._currentUser = null;
    }

    getCurrentUser(): User | null {
        return this._currentUser;
    }

    async isLoggedIn(): Promise<boolean> {
        try {
            const currentUser = await LoginService.getApiLoginCurrent();
            if (currentUser) {
                this._currentUser = {
                    id: currentUser.id!.toString(),
                    name: currentUser.username!
                };
                return true;
            }
            return false;
        } catch {
            return false;
        }
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

    private async getCredentialFromUser(options: CredentialCreateOptions): Promise<PublicKeyCredential> {
        return await navigator.credentials.create({
            publicKey: {
                timeout: options.timeout,
                challenge: this.coerceToArrayBuffer(options.challenge, "challenge"),
                user: {
                    id: this.coerceToArrayBuffer(options.user.id, "user.id"),
                    name: options.user.name!,
                    displayName: options.user.displayName!
                },
                rp: {
                    name: options.rp.name!,
                    id: options.rp.id!
                },
                pubKeyCredParams: options.pubKeyCredParams!.map<PublicKeyCredentialParameters>(param => ({
                    type: "public-key",
                    alg: param.alg!
                })),
                authenticatorSelection: options.authenticatorSelection,
                attestation: options.attestation,
                excludeCredentials: options.excludeCredentials?.map<PublicKeyCredentialDescriptor>(cred => ({
                    type: "public-key",
                    id: this.coerceToArrayBuffer(cred.id, "excludeCredentials.id"),
                })),
            }
        }) as PublicKeyCredential;
    }

    private getAttestationRawResponse(credential: PublicKeyCredential, username: string): CredentialRegistrationRequest {
        const authAttestationResponse = credential.response as AuthenticatorAttestationResponse;
        const authAttestationRawResponse: CredentialRegistrationRequest =
        {
            attestation: {
            id: credential.id,
            rawId: this.coerceToBase64Url(credential.rawId) as string,
            type: PublicKeyCredentialType.PUBLIC_KEY,
            response: {
                clientDataJSON: this.coerceToBase64Url(authAttestationResponse.clientDataJSON) as string,
                attestationObject: this.coerceToBase64Url(authAttestationResponse.attestationObject) as string,
                transports: authAttestationResponse.getTransports ? (authAttestationResponse.getTransports() as AuthenticatorTransport[]) : [],
            },
            clientExtensionResults: UserService.mapClientExtensionResults(credential.getClientExtensionResults())
        },
        credentialName: `${username} credential`
        };

        return authAttestationRawResponse;
    }

    // eslint-disable-next-line complexity
    private static mapClientExtensionResults(results: unknown): import("../clients/UserApiClient/models/AuthenticationExtensionsClientOutputs").AuthenticationExtensionsClientOutputs {
        if (!results) return {};
        // Deep clone and convert BufferSource properties to base64url strings
        const clone = JSON.parse(JSON.stringify(results));
        if (clone.prf && clone.prf.results) {
            // Convert BufferSource to base64url string for 'first'
            if (clone.prf.results.first && (clone.prf.results.first instanceof ArrayBuffer || ArrayBuffer.isView(clone.prf.results.first))) {
                clone.prf.results.first = UserService.prototype.coerceToBase64Url(clone.prf.results.first);
            }
            // Convert BufferSource to base64url string for 'second'
            if (clone.prf.results.second && (clone.prf.results.second instanceof ArrayBuffer || ArrayBuffer.isView(clone.prf.results.second))) {
                clone.prf.results.second = UserService.prototype.coerceToBase64Url(clone.prf.results.second);
            }
        }
        // Ensure 'first' and 'second' are string | null
        if (clone.prf && clone.prf.results) {
            if (clone.prf.results.first && typeof clone.prf.results.first !== "string") {
                clone.prf.results.first = null;
            }
            if (clone.prf.results.second && typeof clone.prf.results.second !== "string") {
                clone.prf.results.second = null;
            }
        }
        return clone;
    }

    coerceToArrayBuffer = function (thing: unknown, name: unknown) {
        if (typeof thing === "string") {
            // base64url to base64
            thing = thing.replace(/-/g, "+").replace(/_/g, "/");

            // base64 to Uint8Array
            const str = window.atob(thing as string);
            const bytes = new Uint8Array(str.length);
            for (let i = 0; i < str.length; i++) {
                bytes[i] = str.charCodeAt(i);
            }
            thing = bytes;
        }

        // Array to Uint8Array
        if (Array.isArray(thing)) {
            thing = new Uint8Array(thing);
        }

        // Uint8Array to ArrayBuffer
        if (thing instanceof Uint8Array) {
            thing = thing.buffer;
        }

        // error if none of the above worked
        if (!(thing instanceof ArrayBuffer)) {
            throw new TypeError("could not coerce '" + name + "' to ArrayBuffer");
        }

        return thing;
    };


    coerceToBase64Url = function (thing: unknown) {
        // Array or ArrayBuffer to Uint8Array
        if (Array.isArray(thing)) {
            thing = Uint8Array.from(thing);
        }

        if (thing instanceof ArrayBuffer) {
            thing = new Uint8Array(thing);
        }

        // Uint8Array to base64
        if (thing instanceof Uint8Array) {
            let str = "";
            const len = thing.byteLength;

            for (let i = 0; i < len; i++) {
                str += String.fromCharCode(thing[i]);
            }
            thing = window.btoa(str);
        }

        if (typeof thing !== "string") {
            throw new Error("could not coerce to string");
        }

        // base64 to base64url
        // NOTE: "=" at the end of challenge is optional, strip it off here
        thing = thing.replace(/\+/g, "-").replace(/\//g, "_").replace(/=*$/g, "");

        return thing;
    };
}