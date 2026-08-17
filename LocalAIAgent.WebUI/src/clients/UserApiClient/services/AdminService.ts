/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreatedInvitationDto } from '../models/CreatedInvitationDto';
import type { InvitationDto } from '../models/InvitationDto';
import type { ManagedUserDto } from '../models/ManagedUserDto';
import type { UpdateManagedUserDto } from '../models/UpdateManagedUserDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AdminService {
    /**
     * @returns CreatedInvitationDto OK
     * @throws ApiError
     */
    public static postApiAdminInvitations(): CancelablePromise<CreatedInvitationDto> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/admin/invitations',
        });
    }
    /**
     * @returns InvitationDto OK
     * @throws ApiError
     */
    public static getApiAdminInvitations(): CancelablePromise<Array<InvitationDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/admin/invitations',
        });
    }
    /**
     * @param invitationId
     * @returns any OK
     * @throws ApiError
     */
    public static deleteApiAdminInvitations(
        invitationId: number,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/admin/invitations/{invitationId}',
            path: {
                'invitationId': invitationId,
            },
        });
    }
    /**
     * @returns ManagedUserDto OK
     * @throws ApiError
     */
    public static getApiAdminUsers(): CancelablePromise<Array<ManagedUserDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/admin/users',
        });
    }
    /**
     * @param userId
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static patchApiAdminUsers(
        userId: number,
        requestBody?: UpdateManagedUserDto,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'PATCH',
            url: '/api/admin/users/{userId}',
            path: {
                'userId': userId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
