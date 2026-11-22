import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useNetInfo } from '@react-native-community/netinfo';
import { api } from '../api/client';
import type { AppointmentListItem, ContentManifest } from '../api/types';
import { subscribeRealtime } from '../realtime';
import { pendingCount } from '../db/outbox';
import { pullDay, flushOutbox } from '../sync/sync';
import { MANIFEST_CACHE_KEY, readCache, writeCache } from '../storage/cache';
import { colors, radius, spacing } from '../theme';

type Props = {
  displayName: string;
  onLogout: () => Promise<void>;
  onOpenAppointment: (item: AppointmentListItem) => void;
  onAdd: () => void;
  onReports: () => void;
  onManifest: (manifest: ContentManifest) => void;
};

const weekDays = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];
const months = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];

function dateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function formatDate(date: Date): string {
  return `${date.getDate()} ${months[date.getMonth()]} ${date.getFullYear()}, ${weekDays[date.getDay()]}`;
}

export function TodayScreen({ displayName, onLogout, onOpenAppointment, onAdd, onReports, onManifest }: Props) {
  const netInfo = useNetInfo();
  const [date, setDate] = useState(() => new Date());
  const [items, setItems] = useState<AppointmentListItem[]>([]);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastSync, setLastSync] = useState<string | null>(null);
  const [pending, setPending] = useState(0);

  const key = dateKey(date);
  const offline = netInfo.isConnected === false;

  const load = useCallback(async (target: Date, isRefresh: boolean) => {
    const dateKeyValue = dateKey(target);

    if (netInfo.isConnected !== false) {
      const flushed = await flushOutbox();
      if (flushed.sent > 0) {
        setPending(await pendingCount());
      }
      if (flushed.failed > 0 && flushed.lastError) {
        setError(flushed.lastError);
      }
    }

    setLoading(!isRefresh);
    setError(null);

    const result = await pullDay(dateKeyValue, netInfo.isConnected !== false);
    setItems(result.items);
    setPending(result.pending);
    setLastSync(new Date().toISOString());

    if (result.error) {
      setError(result.error);
    }

    if (netInfo.isConnected !== false) {
      try {
        const content = await api.getManifest();
        setManifest(content);
        onManifest(content);
        await writeCache(MANIFEST_CACHE_KEY, content);
      } catch {
        // Manifest alınamazsa eldeki içerik korunur.
      }
    }

    setLoading(false);
    setRefreshing(false);
  }, [netInfo.isConnected, onManifest]);

  useEffect(() => {
    void (async () => {
      const cachedManifest = await readCache<ContentManifest>(MANIFEST_CACHE_KEY);
      if (cachedManifest) setManifest(cachedManifest.value);
      await load(date, false);
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  const wasOffline = useRef(false);

  // Bağlantı geri geldiğinde bekleyen yazmaları gönder ve listeyi tazele.
  useEffect(() => {
    if (offline) {
      wasOffline.current = true;
      return;
    }

    if (wasOffline.current) {
      wasOffline.current = false;
      void load(date, true);
    }
  }, [offline, date, load]);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;

    const unsubscribe = subscribeRealtime((event) => {
      if (event.type === 'reminderDue') return;
      if (event.type === 'appointmentChanged' && event.payload.dateKey !== key) return;

      if (timer) clearTimeout(timer);
      timer = setTimeout(() => void load(date, true), 800);
    });

    return () => {
      unsubscribe();
      if (timer) clearTimeout(timer);
    };
  }, [key, date, load]);

  const shiftDay = (offset: number) => {
    const next = new Date(date);
    next.setDate(next.getDate() + offset);
    setDate(next);
  };

  const onRefresh = () => {
    setRefreshing(true);
    void load(date, true);
  };

  const institution = manifest?.institution.name ?? 'CGP - Randevu Takip';

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <View style={{ flex: 1 }}>
          <Text style={styles.institution}>{institution}</Text>
          <Text style={styles.user}>{displayName}</Text>
        </View>
        <TouchableOpacity onPress={() => void onLogout()} style={styles.logout}>
          <Text style={styles.logoutText}>Çıkış</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.dateBar}>
        <TouchableOpacity onPress={() => shiftDay(-1)} style={styles.navButton}>
          <Text style={styles.navText}>‹</Text>
        </TouchableOpacity>
        <View style={{ alignItems: 'center', flex: 1 }}>
          <Text style={styles.dateText}>{formatDate(date)}</Text>
          <Text style={styles.countText}>
            {items.length} görüşme
            {pending > 0 ? ` · ${pending} bekleyen` : ''}
            {lastSync ? ` · ${new Date(lastSync).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}` : ''}
          </Text>
        </View>
        <TouchableOpacity onPress={() => shiftDay(1)} style={styles.navButton}>
          <Text style={styles.navText}>›</Text>
        </TouchableOpacity>
      </View>

      {offline || error ? (
        <View style={styles.offlineBar}>
          <Text style={styles.offlineText}>
            {offline ? 'Çevrimdışı · son kaydedilen liste gösteriliyor' : error}
          </Text>
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator style={{ marginTop: spacing.xl }} color={colors.primary} />
      ) : (
        <FlatList
          data={items}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ padding: spacing.md, paddingBottom: spacing.xl }}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />
          }
          ListEmptyComponent={
            <View style={styles.empty}>
              <Text style={styles.emptyTitle}>Bu gün için görüşme yok</Text>
              <Text style={styles.emptyText}>Yeni görüşme eklemek için masaüstü veya web sürümünü kullanın.</Text>
            </View>
          }
          renderItem={({ item }) => (
            <TouchableOpacity style={styles.card} onPress={() => onOpenAppointment(item)}>
              <View style={[styles.timeBadge, { backgroundColor: item.categoryColorHex }]}>
                <Text style={styles.timeText}>{item.timeLabel}</Text>
                <Text style={styles.timeEnd}>{item.endTimeLabel}</Text>
              </View>

              <View style={{ flex: 1 }}>
                <Text style={styles.person}>{item.guestSummaryText}</Text>
                <Text style={styles.meta}>
                  {item.categoryName} · {item.hostNameWithTitle}
                  {item.roomName ? ` · ${item.roomName}` : ''}
                </Text>
                {item.participantCount > 1 ? (
                  <Text style={styles.meta}>Katılımcılar: {item.participantNames}</Text>
                ) : null}
                {item.studentFullName ? (
                  <Text style={styles.meta}>Öğrenci: {item.studentFullName}</Text>
                ) : null}
                <Text style={styles.meta}>
                  {item.methodText} · {item.phoneE164}
                </Text>
              </View>

              <View style={styles.statusPill}>
                <Text style={styles.statusText}>{item.statusText}</Text>
              </View>
            </TouchableOpacity>
          )}
        />
      )}

      <View style={styles.footer}>
        <TouchableOpacity testID="open-add" style={styles.footerButton} onPress={onAdd}>
          <Text style={styles.footerText}>+ Yeni görüşme</Text>
        </TouchableOpacity>
        <TouchableOpacity testID="open-reports" style={styles.footerButton} onPress={onReports}>
          <Text style={styles.footerText}>Raporlar</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingTop: 56,
    paddingBottom: spacing.md,
    backgroundColor: colors.surface,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  institution: { fontSize: 17, fontWeight: '700', color: colors.text },
  user: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  logout: { paddingHorizontal: spacing.sm, paddingVertical: spacing.xs },
  logoutText: { color: colors.primary, fontWeight: '600', fontSize: 13 },
  dateBar: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.md,
    backgroundColor: colors.surface,
  },
  navButton: { paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  navText: { fontSize: 26, color: colors.primary, lineHeight: 28 },
  dateText: { fontSize: 15, fontWeight: '600', color: colors.text },
  countText: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  offlineBar: {
    backgroundColor: '#FBF3E2',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  offlineText: { color: colors.offline, fontSize: 12 },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border,
  },
  timeBadge: {
    width: 62,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    marginRight: spacing.md,
  },
  timeText: { color: '#FFFFFF', fontWeight: '700', fontSize: 15 },
  timeEnd: { color: '#E8FFFFFF', fontSize: 11 },
  person: { fontSize: 15, fontWeight: '600', color: colors.text },
  meta: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  statusPill: {
    backgroundColor: colors.background,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
    marginLeft: spacing.sm,
  },
  statusText: { fontSize: 11, color: colors.textMuted, fontWeight: '600' },
  empty: { alignItems: 'center', marginTop: spacing.xl },
  emptyTitle: { fontSize: 16, fontWeight: '600', color: colors.text },
  emptyText: { fontSize: 13, color: colors.textMuted, marginTop: spacing.sm, textAlign: 'center' },
  footer: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: colors.border,
    backgroundColor: colors.surface,
  },
  footerButton: { flex: 1, alignItems: 'center', paddingVertical: spacing.md },
  footerText: { color: colors.primary, fontWeight: '700', fontSize: 13 },
});
