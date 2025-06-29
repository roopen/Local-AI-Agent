import UserSettings from './UserSettings';

describe('UserSettings', () => {
  it('should add likes and dislikes', () => {
    // Arrange
    const userSettings = new UserSettings();

    // Act
    userSettings.addLike('chocolate');
    userSettings.addLike('vanilla');
    userSettings.addDislike('spinach');
    userSettings.addDislike('broccoli');

    // Assert
    expect(userSettings.likes).toContain('chocolate');
    expect(userSettings.likes).toContain('vanilla');
    expect(userSettings.dislikes).toContain('spinach');
    expect(userSettings.dislikes).toContain('broccoli');
  });

  it('should not add duplicate likes or dislikes', () => {
      // Arrange
      const userSettings = new UserSettings();
      userSettings.addLike('chocolate');
      userSettings.addDislike('spinach');

      // Act
      userSettings.addLike('chocolate'); // Duplicate like
      userSettings.addDislike('spinach'); // Duplicate dislike

      // Assert
      expect(userSettings.likes).toEqual(['chocolate']);
      expect(userSettings.dislikes).toEqual(['spinach']);
  });

  it('should remove likes and dislikes', () => {
      // Arrange
      const userSettings = new UserSettings();
      userSettings.addLike('chocolate');
      userSettings.addDislike('spinach');

      // Act
      userSettings.removeLike('chocolate');
      userSettings.removeDislike('spinach');

      // Assert
      expect(userSettings.likes).not.toContain('chocolate');
      expect(userSettings.dislikes).not.toContain('spinach');
  });

  it('should filter out invalid likes and dislikes', () => {
      // Arrange
      const userSettings = new UserSettings([''], ['invalid', '']);

      // Assert
      expect(userSettings.likes).toEqual([]);
      expect(userSettings.dislikes).toEqual(['invalid']);
  });

  it('should return a summary of likes and dislikes', () => {
      // Arrange
      const userSettings = new UserSettings(['chocolate'], ['spinach']);

      // Act
      const summary = userSettings.getSummary();

      // Assert
      expect(summary).toBe('Likes: chocolate | Dislikes: spinach');
  });
});