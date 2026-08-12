import React, { useState } from "react";
import SettingsComponent from "./SettingsComponent";
import UserService from "../users/UserService";

const SetupComponent: React.FC = () => {
  const [settingsCompleted, setSettingsCompleted] = useState(false);
  const userService = UserService.getInstance();

  const handleSettingsSaved = async () => {
    const user = userService.getCurrentUser();
    const [preferences, aiSettings] = await Promise.all([
      userService.getUserPreferences(user!.id),
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
      <p>Set your news preferences and connect an LLM API to continue.</p>

      <SettingsComponent onSave={handleSettingsSaved} />

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
