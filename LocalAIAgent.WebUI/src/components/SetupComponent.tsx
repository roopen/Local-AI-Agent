import React, { useState } from "react";
import SettingsComponent from "./SettingsComponent";
import UserService from "../users/UserService";

const SetupComponent: React.FC = () => {
  const [settingsCompleted, setSettingsCompleted] = useState(false);
  const userService = UserService.getInstance();

  const handleSettingsSaved = async () => {
    const user = userService.getCurrentUser();
    const preferences = await userService.getUserPreferences(user!.id);

    if (preferences != null && !preferences.isEmpty()) {
        setSettingsCompleted(true);
    }
  }

  return (
    <div style={{ margin: "0 auto" }}>
      <h2>👋 Welcome!</h2>
      <p>Let's set up your preferences so that we can curate news to your liking.</p>

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