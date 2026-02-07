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
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class Fido2Service {
    /**
     * @returns AssertionOptions OK
     * @throws ApiError
     */
    public static postAssertionOptions(): CancelablePromise<AssertionOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/assertionOptions',
        });
    }
    /**
     * @param requestBody
     * @returns AttestationResult OK
     * @throws ApiError
     */
    public static postMakeAssertion(
        requestBody?: AuthenticatorAssertionRawResponse,
    ): CancelablePromise<AttestationResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeAssertion',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns CredentialCreateOptions OK
     * @throws ApiError
     */
    public static postMakeCredentialOptionsExistingUser(): CancelablePromise<CredentialCreateOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeCredentialOptionsExistingUser',
        });
    }
    /**
     * @param requestBody
     * @returns RegisteredPublicKeyCredential OK
     * @throws ApiError
     */
    public static postAddCredentialExistingUser(
        requestBody?: CredentialRegistrationRequest,
    ): CancelablePromise<RegisteredPublicKeyCredential> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/addCredentialExistingUser',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static postRemoveCredential(
        requestBody?: string,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/removeCredential',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns CredentialInfo OK
     * @throws ApiError
     */
    public static getListCredentials(): CancelablePromise<Array<CredentialInfo>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/listCredentials',
        });
    }
    /**
     * @param username
     * @returns CredentialCreateOptions OK
     * @throws ApiError
     */
    public static postMakeCredentialOptions(
        username?: string,
    ): CancelablePromise<CredentialCreateOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeCredentialOptions',
            query: {
                'username': username,
            },
        });
    }
    /**
     * @param requestBody
     * @returns RegisteredPublicKeyCredential OK
     * @throws ApiError
     */
    public static postMakeCredential(
        requestBody?: CredentialRegistrationRequest,
    ): CancelablePromise<RegisteredPublicKeyCredential> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeCredential',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
