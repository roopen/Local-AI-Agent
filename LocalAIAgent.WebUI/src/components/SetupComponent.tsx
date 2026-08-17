import React, { useState } from "react";
import SettingsComponent from "./SettingsComponent";
import UserService from "../users/UserService";

interface SetupComponentProps {
  llmConnectionError?: string | null;
}

const SetupComponent: React.FC<SetupComponentProps> = ({ llmConnectionError }) => {
  const [settingsCompleted, setSettingsCompleted] = useState(false);
  const userService = UserService.getInstance();

  const handleSettingsSaved = async () => {
    const [preferences, aiSettings] = await Promise.all([
      userService.getUserPreferences(),
      userService.getAiSettings(),
    ]);

    if (preferences != null && !preferences.isEmpty() && aiSettings.isConfigured) {
        setSettingsCompleted(true);
    } else {
        setSettingsCompleted(false);
    }
  }

  return (
    <div style={{ margin: "0 auto" }}>
      <h2>👋 Welcome!</h2>
      <p>
        {userService.getCurrentUser()?.role === 'Owner'
          ? 'Set your news preferences and connect the shared LLM API to continue.'
          : 'Set your news preferences. The owner manages the shared LLM connection.'}
      </p>

      <SettingsComponent
        onSave={handleSettingsSaved}
        initialTab={llmConnectionError ? 'llm' : 'prompt'}
        initialLlmError={llmConnectionError}
      />

      {!settingsCompleted && (
        <div style={{ marginTop: "1rem", color: "gray" }}>
          <em>Settings must be completed to continue.</em>
        </div>
      )}
      {settingsCompleted && (
        <button onClick={() => { window.location.href = "/"; }} style={{ marginTop: 10 }}>Let's get started!</button>
      )}
    </div>
  );
};

export default SetupComponent;
