/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { UserPreferenceDto } from '../models/UserPreferenceDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class UserPreferencesService {
    /**
     * @param userId
     * @returns UserPreferenceDto OK
     * @throws ApiError
     */
    public static getApiUserPreferences(
        userId: number,
    ): CancelablePromise<UserPreferenceDto> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/UserPreferences/{userId}',
            path: {
                'userId': userId,
            },
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static postApiSavePreferences(
        requestBody?: UserPreferenceDto,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/SavePreferences',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
