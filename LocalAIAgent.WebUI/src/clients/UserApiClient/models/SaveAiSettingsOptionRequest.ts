/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export type SaveAiSettingsOptionRequest = {
    name: string;
    modelId: string;
    endpointUrl?: string | null;
    connectionSourceSettingsId?: number | null;
    apiKey?: string | null;
    clearApiKey?: boolean;
    useResultsForDataset?: boolean;
    temperature?: number;
    topP?: number;
    frequencyPenalty?: number;
    presencePenalty?: number;
};

