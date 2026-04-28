/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { LMStudioCapabilities } from './LMStudioCapabilities';
import type { LMStudioQuantization } from './LMStudioQuantization';
export type LMStudioModel = {
    type?: string | null;
    publisher?: string | null;
    key?: string | null;
    display_name?: string | null;
    architecture?: string | null;
    quantization?: LMStudioQuantization;
    size_bytes?: number;
    params_string?: string | null;
    max_context_length?: number;
    format?: string | null;
    capabilities?: LMStudioCapabilities;
    description?: string | null;
};

