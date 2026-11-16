import { StatusBar } from 'expo-status-bar';
import React, { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { api } from './src/api/client';
import type { AppointmentListItem, AuthTokens, ContentManifest } from './src/api/types';
import { registerForPushAsync, unregisterPushAsync } from './src/push';
import { startRealtime, stopRealtime, subscribeRealtime } from './src/realtime';
import { AddAppointmentScreen } from './src/screens/AddAppointmentScreen';
import { DetailScreen } from './src/screens/DetailScreen';
import { LoginScreen } from './src/screens/LoginScreen';
import { ReportsScreen } from './src/screens/ReportsScreen';
import { TodayScreen } from './src/screens/TodayScreen';
import { clearSession, loadSession, saveSession } from './src/storage/session';
import { colors, radius, spacing } from './src/theme';

type Screen =
  | { name: 'today' }
  | { name: 'detail'; item: AppointmentListItem }
  | { name: 'add' }
  | { name: 'reports' };

type Banner = { title: string; body: string };

export default function App() {
  const [booting, setBooting] = useState(true);
  const [user, setUser] = useState<AuthTokens['user'] | null>(null);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [screen, setScreen] = useState<Screen>({ name: 'today' });
  const [banner, setBanner] = useState<Banner | null>(null);
  const bannerTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pushToken = useRef<string | null>(null);

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

  useEffect(() => {
    if (!user) {
      void stopRealtime();
      return;
    }

    void startRealtime();

    void registerForPushAsync().then((token) => {
      pushToken.current = token;
    });

    const unsubscribe = subscribeRealtime((event) => {
      if (event.type !== 'reminderDue') return;

      setBanner({ title: event.payload.title, body: event.payload.body });

      if (bannerTimer.current) clearTimeout(bannerTimer.current);
      bannerTimer.current = setTimeout(() => setBanner(null), 9000);
    });

    return () => {
      unsubscribe();
      if (bannerTimer.current) clearTimeout(bannerTimer.current);
      void stopRealtime();
    };
  }, [user]);

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
    await unregisterPushAsync(pushToken.current);
    pushToken.current = null;
    await api.logout();
    await clearSession();
    setUser(null);
    setManifest(null);
    setBanner(null);
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

      {banner ? (
        <TouchableOpacity style={styles.banner} onPress={() => setBanner(null)} activeOpacity={0.9}>
          <Text style={styles.bannerTitle}>{banner.title}</Text>
          <Text style={styles.bannerBody}>{banner.body}</Text>
        </TouchableOpacity>
      ) : null}
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
  banner: {
    position: 'absolute',
    left: spacing.md,
    right: spacing.md,
    bottom: 28,
    backgroundColor: colors.surface,
    borderLeftWidth: 4,
    borderLeftColor: colors.primary,
    borderRadius: radius.md,
    padding: spacing.md,
    shadowColor: '#000',
    shadowOpacity: 0.12,
    shadowRadius: 10,
    shadowOffset: { width: 0, height: 4 },
    elevation: 6,
  },
  bannerTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: colors.text,
  },
  bannerBody: {
    fontSize: 12,
    color: colors.textMuted,
    marginTop: 3,
  },
});
