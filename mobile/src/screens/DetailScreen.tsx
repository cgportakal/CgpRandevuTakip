import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useNetInfo } from '@react-native-community/netinfo';
import { api } from '../api/client';
import type { AppointmentDetail, AppointmentListItem, AppointmentOutcomeInput } from '../api/types';
import { enqueue, pendingCount } from '../db/outbox';
import { flushOutbox } from '../sync/sync';
import { colors, radius, spacing } from '../theme';

type Props = {
  item: AppointmentListItem;
  onBack: () => void;
  onChanged: () => void;
};

export function DetailScreen({ item, onBack, onChanged }: Props) {
  const netInfo = useNetInfo();
  const online = netInfo.isConnected !== false;
  const [detail, setDetail] = useState<AppointmentDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [pending, setPending] = useState(0);

  useEffect(() => {
    void (async () => {
      setPending(await pendingCount());
      if (!online) return;

      setLoading(true);
      try {
        setDetail(await api.getDetail(item.id));
      } catch {
        // Çevrimdışı/hatalı durumda listedeki özet bilgi gösterilir.
      } finally {
        setLoading(false);
      }
    })();
  }, [item.id, online]);

  const saveOutcome = async (input: AppointmentOutcomeInput) => {
    if (!online) {
      await enqueue('appointment.outcome', { id: item.id, input }, item.id);
      setPending(await pendingCount());
      Alert.alert('Kuyruğa alındı', 'Bağlantı gelince sonuç sunucuya gönderilecek.');
      return;
    }

    try {
      await api.saveOutcome(item.id, input);
      await flushOutbox();
      Alert.alert('Kaydedildi', 'Görüşme sonucu güncellendi.');
      onChanged();
    } catch (error) {
      Alert.alert('Hata', error instanceof Error ? error.message : 'Sonuç kaydedilemedi.');
    }
  };

  const cancelSeries = () => {
    const seriesId = detail?.seriesId;
    if (!seriesId) return;

    Alert.alert('Seriyi iptal et', 'Gelecek tarihli planlanmış görüşmeler iptal edilecek.', [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'İptal et',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            if (!online) {
              await enqueue('series.cancel', { seriesId }, seriesId);
              setPending(await pendingCount());
              Alert.alert('Kuyruğa alındı', 'Bağlantı gelince seri iptal edilecek.');
              return;
            }

            try {
              const result = await api.cancelSeries(seriesId);
              Alert.alert('Seri', result.summary);
              onChanged();
            } catch (error) {
              Alert.alert('Hata', error instanceof Error ? error.message : 'Seri iptal edilemedi.');
            }
          })();
        },
      },
    ]);
  };

  const outcome = (attendance: AppointmentOutcomeInput['attendance'], closed: boolean) =>
    saveOutcome({
      attendance,
      summary: null,
      willRepeat: false,
      postNotes: null,
      isClosed: closed,
    });

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={onBack} style={styles.back}>
          <Text style={styles.backText}>‹ Geri</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Görüşme detayı</Text>
      </View>

      <View style={styles.body}>
        <Text style={styles.person}>{detail?.personFullName ?? item.personFullName}</Text>
        <Text style={styles.meta}>{detail?.dateLabel ?? ''} · {detail?.timeRangeLabel ?? item.timeLabel}</Text>
        <Text style={styles.meta}>{detail?.phoneE164 ?? item.phoneE164}</Text>

        <View style={styles.card}>
          <Text style={styles.label}>GÖRÜŞMECİ</Text>
          <Text style={styles.value}>{detail?.hostName ?? item.hostName}</Text>
          <Text style={styles.label}>KATEGORİ</Text>
          <Text style={styles.value}>{detail?.categoryName ?? item.categoryName}</Text>
          <Text style={styles.label}>DURUM</Text>
          <Text style={styles.value}>{detail?.statusText ?? item.statusText}</Text>
          {detail?.seriesId ? (
            <Text style={styles.series}>Seri {detail.seriesIndex}/{detail.seriesCount}</Text>
          ) : null}
        </View>

        {loading ? <ActivityIndicator color={colors.primary} /> : null}

        <Text style={styles.section}>Sonuç gir</Text>
        <View style={styles.actions}>
          <TouchableOpacity style={styles.action} onPress={() => void outcome('Arrived', true)}>
            <Text style={styles.actionText}>Tamamlandı</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.action} onPress={() => void outcome('NoShow', true)}>
            <Text style={styles.actionText}>Gelmedi</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.action} onPress={() => void outcome('Postponed', false)}>
            <Text style={styles.actionText}>Ertelendi</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.actionDanger} onPress={() => void outcome('Cancelled', true)}>
            <Text style={styles.actionDangerText}>İptal</Text>
          </TouchableOpacity>
        </View>

        {detail?.seriesId ? (
          <TouchableOpacity style={styles.seriesButton} onPress={cancelSeries}>
            <Text style={styles.seriesButtonText}>Seriyi iptal et</Text>
          </TouchableOpacity>
        ) : null}

        <Text style={styles.meta}>
          {online ? 'Çevrimiçi' : 'Çevrimdışı'} · Bekleyen işlem: {pending}
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingTop: 56,
    paddingBottom: spacing.md,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.surface,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  back: { paddingRight: spacing.md },
  backText: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  title: { fontSize: 16, fontWeight: '700', color: colors.text },
  body: { padding: spacing.md },
  person: { fontSize: 20, fontWeight: '700', color: colors.text },
  meta: { fontSize: 12, color: colors.textMuted, marginTop: 4 },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    marginTop: spacing.md,
  },
  label: { fontSize: 10, fontWeight: '700', color: colors.textMuted, marginTop: spacing.sm },
  value: { fontSize: 14, color: colors.text, marginTop: 2 },
  series: { fontSize: 12, color: colors.primary, fontWeight: '600', marginTop: spacing.sm },
  section: { fontSize: 14, fontWeight: '700', color: colors.text, marginTop: spacing.lg },
  actions: { flexDirection: 'row', flexWrap: 'wrap', marginTop: spacing.sm },
  action: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    marginRight: spacing.sm,
    marginBottom: spacing.sm,
  },
  actionText: { fontSize: 12, color: colors.text, fontWeight: '600' },
  actionDanger: {
    backgroundColor: '#FBEAE8',
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    marginBottom: spacing.sm,
  },
  actionDangerText: { fontSize: 12, color: colors.danger, fontWeight: '600' },
  seriesButton: {
    backgroundColor: '#FBEAE8',
    borderRadius: radius.md,
    padding: spacing.md,
    marginTop: spacing.sm,
    alignItems: 'center',
  },
  seriesButtonText: { color: colors.danger, fontWeight: '700' },
});
