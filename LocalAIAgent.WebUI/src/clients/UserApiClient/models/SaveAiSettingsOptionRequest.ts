/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export type SaveAiSettingsOptionRequest = {
    name: string;
    modelId: string;
    endpointUrl: string;
    apiKey?: string | null;
    clearApiKey?: boolean;
    temperature?: number;
    topP?: number;
    frequencyPenalty?: number;
    presencePenalty?: number;
};

