import { StatusBar } from 'expo-status-bar';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { api } from './src/api/client';
import type { AuthTokens } from './src/api/types';
import { LoginScreen } from './src/screens/LoginScreen';
import { TodayScreen } from './src/screens/TodayScreen';
import { clearSession, loadSession, saveSession } from './src/storage/session';
import { colors } from './src/theme';

export default function App() {
  const [booting, setBooting] = useState(true);
  const [user, setUser] = useState<AuthTokens['user'] | null>(null);

  useEffect(() => {
    void (async () => {
      const session = await loadSession();
      if (session) {
        api.setTokens(session.accessToken, session.refreshToken);
        setUser(session.user);
      }
      setBooting(false);
    })();
  }, []);

  const onSignedIn = async (tokens: AuthTokens) => {
    api.setTokens(tokens.accessToken, tokens.refreshToken);
    await saveSession({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      user: tokens.user,
    });
    setUser(tokens.user);
  };

  const onLogout = async () => {
    await api.logout();
    await clearSession();
    setUser(null);
  };

  if (booting) {
    return (
      <View style={styles.boot}>
        <StatusBar style="dark" />
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  return (
    <>
      <StatusBar style="dark" />
      {user ? (
        <TodayScreen displayName={user.displayName} onLogout={onLogout} />
      ) : (
        <LoginScreen onSignedIn={onSignedIn} />
      )}
    </>
  );
}

const styles = StyleSheet.create({
  boot: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.background,
  },
});
