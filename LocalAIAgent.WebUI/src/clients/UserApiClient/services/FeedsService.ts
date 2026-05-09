/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AddCustomFeedDto } from '../models/AddCustomFeedDto';
import type { FeedDto } from '../models/FeedDto';
import type { LanguageOptionDto } from '../models/LanguageOptionDto';
import type { ToggleFeedDto } from '../models/ToggleFeedDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class FeedsService {
    /**
     * @returns LanguageOptionDto OK
     * @throws ApiError
     */
    public static getApiFeedsLanguages(): CancelablePromise<Array<LanguageOptionDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Feeds/Languages',
        });
    }
    /**
     * @param userId
     * @returns FeedDto OK
     * @throws ApiError
     */
    public static getApiFeeds(
        userId: number,
    ): CancelablePromise<Array<FeedDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Feeds/{userId}',
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
    public static postApiFeedsToggle(
        requestBody?: ToggleFeedDto,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/Feeds/Toggle',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param requestBody
     * @returns FeedDto OK
     * @throws ApiError
     */
    public static postApiFeedsCustom(
        requestBody?: AddCustomFeedDto,
    ): CancelablePromise<FeedDto> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/Feeds/Custom',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param customFeedId
     * @param userId
     * @returns any OK
     * @throws ApiError
     */
    public static deleteApiFeedsCustom(
        customFeedId: number,
        userId?: number,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/Feeds/Custom/{customFeedId}',
            path: {
                'customFeedId': customFeedId,
            },
            query: {
                'userId': userId,
            },
        });
    }
}
