import React, { useState, useEffect } from 'react';
import AISettings from '../domain/AISettings';
import { Button } from '@progress/kendo-react-buttons';
import { TextBox, Slider, SliderLabel } from '@progress/kendo-react-inputs';
import UserService from '../users/UserService';

const AISettingsComponent: React.FC = () => {
    const [settings, setSettings] = useState<AISettings>(new AISettings());
    const [statusMessage, setStatusMessage] = useState('');
    const [statusStyle, setStatusStyle] = useState<React.CSSProperties>({ opacity: 0 });

    useEffect(() => {
        const fetchSettings = async () => {
            const settings = await UserService.getInstance().getAiSettings();
            setSettings(settings ?? new AISettings());
        };
        fetchSettings();
    }, []);

    const handleSave = async () => {
        await UserService.getInstance().saveAiSettings(settings);

        setStatusMessage('Settings Saved!');
        setStatusStyle({ opacity: 1, transition: 'opacity 0.2s ease-in' });

        setTimeout(() => {
            setStatusStyle({ opacity: 0, transition: 'opacity 0.5s ease-out' });
        }, 2000);
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const handleChange = (field: keyof AISettings, value: any) => {
        const newSettings = new AISettings(
            settings.modelId, 
            settings.apikey, 
            settings.endpointUrl, 
            settings.temperature, 
            settings.topP, 
            settings.frequencyPenalty, 
            settings.presencePenalty);

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (newSettings as any)[field] = value;
        setSettings(newSettings);
    };

    return (
        <div style={{ padding: '20px', borderRadius: '5px' }}>
            <h2 style={{ color: 'white', marginTop: 0 }}>AI Configuration</h2>
            
            <div style={{ marginBottom: '15px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '5px' }}>Model ID</label>
                <TextBox
                    value={settings.modelId}
                    onChange={(e) => handleChange('modelId', e.value)}
                    placeholder="e.g. gpt-4, llama-2-7b"
                    style={{ width: '50%' }}
                />
            </div>

            <div style={{ marginBottom: '15px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '5px' }}>API Key</label>
                <TextBox
                    value={settings.apikey}
                    onChange={(e) => handleChange('apikey', e.value)}
                    placeholder="Enter your API key"
                    style={{ width: '50%' }}
                />
            </div>

            <div style={{ marginBottom: '15px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '5px' }}>Endpoint URL</label>
                <TextBox
                    value={settings.endpointUrl}
                    onChange={(e) => handleChange('endpointUrl', e.value)}
                    placeholder="https://api.openai.com/v1"
                    style={{ width: '50%' }}
                />
            </div>

            <div style={{ marginBottom: '25px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '10px' }}>
                    Temperature: {settings.temperature}
                </label>
                <Slider
                    value={settings.temperature}
                    min={0}
                    max={2}
                    step={0.1}
                    buttons={true}
                    onChange={(e) => handleChange('temperature', e.value)}
                    style={{ width: '50%' }}
                >
                    <SliderLabel position={0}>0</SliderLabel>
                    <SliderLabel position={1}>1</SliderLabel>
                    <SliderLabel position={2}>2</SliderLabel>
                </Slider>
            </div>

            <div style={{ marginBottom: '25px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '10px' }}>
                    TopP: {settings.topP}
                </label>
                <Slider
                    value={settings.topP}
                    min={0}
                    max={2}
                    step={0.1}
                    buttons={true}
                    onChange={(e) => handleChange('topP', e.value)}
                    style={{ width: '50%' }}
                >
                    <SliderLabel position={0}>0</SliderLabel>
                    <SliderLabel position={1}>1</SliderLabel>
                    <SliderLabel position={2}>2</SliderLabel>
                </Slider>
            </div>

            <div style={{ marginBottom: '25px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '10px' }}>
                    FrequencyPenalty: {settings.frequencyPenalty}
                </label>
                <Slider
                    value={settings.frequencyPenalty}
                    min={0}
                    max={2}
                    step={0.1}
                    buttons={true}
                    onChange={(e) => handleChange('frequencyPenalty', e.value)}
                    style={{ width: '50%' }}
                >
                    <SliderLabel position={0}>0</SliderLabel>
                    <SliderLabel position={1}>1</SliderLabel>
                    <SliderLabel position={2}>2</SliderLabel>
                </Slider>
            </div>

            <div style={{ marginBottom: '25px' }}>
                <label style={{ color: 'white', display: 'block', marginBottom: '10px' }}>
                    PresenceyPenalty: {settings.presencePenalty}
                </label>
                <Slider
                    value={settings.presencePenalty}
                    min={0}
                    max={2}
                    step={0.1}
                    buttons={true}
                    onChange={(e) => handleChange('presencePenalty', e.value)}
                    style={{ width: '50%' }}
                >
                    <SliderLabel position={0}>0</SliderLabel>
                    <SliderLabel position={1}>1</SliderLabel>
                    <SliderLabel position={2}>2</SliderLabel>
                </Slider>
            </div>

            <div style={{ display: 'flex', alignItems: 'center' }}>
                <Button themeColor={'primary'} onClick={handleSave}>
                    Save AI Settings
                </Button>
                <span style={{ color: '#4cd964', marginLeft: '10px', ...statusStyle }}>
                    {statusMessage}
                </span>
            </div>
        </div>
    );
};

export default AISettingsComponent;
