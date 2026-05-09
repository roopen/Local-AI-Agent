/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { FeedDto } from '../models/FeedDto';
import type { ToggleFeedDto } from '../models/ToggleFeedDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class FeedsService {
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
}
