import { StatusBar } from 'expo-status-bar';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { api } from './src/api/client';
import type { AppointmentListItem, AuthTokens, ContentManifest } from './src/api/types';
import { AddAppointmentScreen } from './src/screens/AddAppointmentScreen';
import { DetailScreen } from './src/screens/DetailScreen';
import { LoginScreen } from './src/screens/LoginScreen';
import { ReportsScreen } from './src/screens/ReportsScreen';
import { TodayScreen } from './src/screens/TodayScreen';
import { clearSession, loadSession, saveSession } from './src/storage/session';
import { colors } from './src/theme';

type Screen =
  | { name: 'today' }
  | { name: 'detail'; item: AppointmentListItem }
  | { name: 'add' }
  | { name: 'reports' };

export default function App() {
  const [booting, setBooting] = useState(true);
  const [user, setUser] = useState<AuthTokens['user'] | null>(null);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [screen, setScreen] = useState<Screen>({ name: 'today' });

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
    setScreen({ name: 'today' });
  };

  const onLogout = async () => {
    await api.logout();
    await clearSession();
    setUser(null);
    setManifest(null);
    setScreen({ name: 'today' });
  };

  if (booting) {
    return (
      <View style={styles.boot}>
        <StatusBar style="dark" />
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  const renderSignedIn = () => {
    switch (screen.name) {
      case 'detail':
        return (
          <DetailScreen
            item={screen.item}
            onBack={() => setScreen({ name: 'today' })}
            onChanged={() => setScreen({ name: 'today' })}
          />
        );

      case 'add':
        return (
          <AddAppointmentScreen
            manifest={manifest}
            onBack={() => setScreen({ name: 'today' })}
            onSaved={() => setScreen({ name: 'today' })}
          />
        );

      case 'reports':
        return <ReportsScreen onBack={() => setScreen({ name: 'today' })} />;

      default:
        return (
          <TodayScreen
            displayName={user!.displayName}
            onLogout={onLogout}
            onOpenAppointment={(item) => setScreen({ name: 'detail', item })}
            onAdd={() => setScreen({ name: 'add' })}
            onReports={() => setScreen({ name: 'reports' })}
            onManifest={setManifest}
          />
        );
    }
  };

  return (
    <>
      <StatusBar style="dark" />
      {user ? renderSignedIn() : <LoginScreen onSignedIn={onSignedIn} />}
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
