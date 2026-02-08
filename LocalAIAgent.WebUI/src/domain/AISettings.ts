export default class AISettings {
    modelId: string;
    apikey: string;
    endpointUrl: string;
    temperature: number;
    topP: number;
    frequencyPenalty: number;
    presencePenalty: number;

    constructor(
        modelId: string = 'gemma-3-27b-it-qat', 
        apikey: string = '', 
        endpointUrl: string = 'http://localhost:1234/v1/', 
        temperature: number = 0.2,
        topP: number = 1.0,
        frequencyPenalty: number = 1.0,
        presencePenalty: number = 1.0) {
        this.modelId = modelId;
        this.apikey = apikey;
        this.endpointUrl = endpointUrl;
        this.temperature = temperature;
        this.topP = topP;
        this.frequencyPenalty = frequencyPenalty;
        this.presencePenalty = presencePenalty;
    }
}
