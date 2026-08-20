/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AiSettingsOptionResponse } from './AiSettingsOptionResponse';
export type AiSettingsCatalogResponse = {
    isConfigured?: boolean;
    isOwner?: boolean;
    selectedSettingsId?: number | null;
    options: Array<AiSettingsOptionResponse> | null;
};

