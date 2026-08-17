/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AssertionOptions } from '../models/AssertionOptions';
import type { AttestationResult } from '../models/AttestationResult';
import type { AuthenticatorAssertionRawResponse } from '../models/AuthenticatorAssertionRawResponse';
import type { CredentialCreateOptions } from '../models/CredentialCreateOptions';
import type { CredentialInfo } from '../models/CredentialInfo';
import type { CredentialRegistrationRequest } from '../models/CredentialRegistrationRequest';
import type { RegisteredPublicKeyCredential } from '../models/RegisteredPublicKeyCredential';
import type { RegistrationOptionsRequest } from '../models/RegistrationOptionsRequest';
import type { RegistrationStatusDto } from '../models/RegistrationStatusDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class Fido2Service {
    /**
     * @returns AssertionOptions OK
     * @throws ApiError
     */
    public static postApiAuthLoginOptions(): CancelablePromise<AssertionOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/login/options',
        });
    }
    /**
     * @param requestBody
     * @returns AttestationResult OK
     * @throws ApiError
     */
    public static postApiAuthLoginComplete(
        requestBody?: AuthenticatorAssertionRawResponse,
    ): CancelablePromise<AttestationResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/login/complete',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns CredentialCreateOptions OK
     * @throws ApiError
     */
    public static postApiAuthPasskeysOptions(): CancelablePromise<CredentialCreateOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/passkeys/options',
        });
    }
    /**
     * @param requestBody
     * @returns RegisteredPublicKeyCredential OK
     * @throws ApiError
     */
    public static postApiAuthPasskeys(
        requestBody?: CredentialRegistrationRequest,
    ): CancelablePromise<RegisteredPublicKeyCredential> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/passkeys',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns CredentialInfo OK
     * @throws ApiError
     */
    public static getApiAuthPasskeys(): CancelablePromise<Array<CredentialInfo>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/auth/passkeys',
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static postApiAuthPasskeysRemove(
        requestBody?: string,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/passkeys/remove',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns RegistrationStatusDto OK
     * @throws ApiError
     */
    public static getApiAuthRegistrationStatus(): CancelablePromise<RegistrationStatusDto> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/auth/registration-status',
        });
    }
    /**
     * @param requestBody
     * @returns CredentialCreateOptions OK
     * @throws ApiError
     */
    public static postApiAuthRegisterOptions(
        requestBody?: RegistrationOptionsRequest,
    ): CancelablePromise<CredentialCreateOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/register/options',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param requestBody
     * @returns RegisteredPublicKeyCredential OK
     * @throws ApiError
     */
    public static postApiAuthRegisterComplete(
        requestBody?: CredentialRegistrationRequest,
    ): CancelablePromise<RegisteredPublicKeyCredential> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/auth/register/complete',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
