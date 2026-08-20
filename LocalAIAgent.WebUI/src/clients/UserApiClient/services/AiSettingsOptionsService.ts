/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AiSettingsCatalogResponse } from '../models/AiSettingsCatalogResponse';
import type { AiSettingsOptionResponse } from '../models/AiSettingsOptionResponse';
import type { SaveAiSettingsOptionRequest } from '../models/SaveAiSettingsOptionRequest';
import type { SelectAiSettingsRequest } from '../models/SelectAiSettingsRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiSettingsOptionsService {
    /**
     * @returns AiSettingsCatalogResponse OK
     * @throws ApiError
     */
    public static getApiAiSettingsOptions(): CancelablePromise<AiSettingsCatalogResponse> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-settings/options',
        });
    }
    /**
     * @param requestBody
     * @returns AiSettingsOptionResponse OK
     * @throws ApiError
     */
    public static postApiAiSettingsOptions(
        requestBody?: SaveAiSettingsOptionRequest,
    ): CancelablePromise<AiSettingsOptionResponse> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/ai-settings/options',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param settingsId
     * @param requestBody
     * @returns AiSettingsOptionResponse OK
     * @throws ApiError
     */
    public static putApiAiSettingsOptions(
        settingsId: number,
        requestBody?: SaveAiSettingsOptionRequest,
    ): CancelablePromise<AiSettingsOptionResponse> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/ai-settings/options/{settingsId}',
            path: {
                'settingsId': settingsId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param settingsId
     * @returns any OK
     * @throws ApiError
     */
    public static deleteApiAiSettingsOptions(
        settingsId: number,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/ai-settings/options/{settingsId}',
            path: {
                'settingsId': settingsId,
            },
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static putApiAiSettingsSelection(
        requestBody?: SelectAiSettingsRequest,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/ai-settings/selection',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
