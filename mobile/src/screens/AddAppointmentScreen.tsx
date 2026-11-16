import React, { useMemo, useState } from 'react';
import {
  Alert,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useNetInfo } from '@react-native-community/netinfo';
import { api } from '../api/client';
import type {
  AppointmentInput,
  AppointmentMethod,
  ContentManifest,
  SeriesFrequency,
  SeriesPlan,
} from '../api/types';
import { enqueue } from '../db/outbox';
import { flushOutbox } from '../sync/sync';
import { colors, radius, spacing } from '../theme';

type Props = {
  manifest: ContentManifest | null;
  onBack: () => void;
  onSaved: () => void;
};

const methods: { value: AppointmentMethod; label: string }[] = [
  { value: 'FaceToFace', label: 'Yüz yüze' },
  { value: 'Phone', label: 'Telefon' },
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'Online', label: 'İnternet' },
];

const frequencies: { value: SeriesFrequency; label: string }[] = [
  { value: 'Weekly', label: 'Her hafta' },
  { value: 'Biweekly', label: '2 haftada bir' },
  { value: 'Monthly', label: 'Her ay' },
];

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function todayKey(): string {
  const now = new Date();
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

function nextHour(): string {
  const now = new Date();
  now.setHours(now.getHours() + 1, 0, 0, 0);
  return `${pad(now.getHours())}:${pad(now.getMinutes())}`;
}

export function AddAppointmentScreen({ manifest, onBack, onSaved }: Props) {
  const netInfo = useNetInfo();
  const online = netInfo.isConnected !== false;

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [date, setDate] = useState(todayKey());
  const [time, setTime] = useState(nextHour());
  const [duration, setDuration] = useState('30');
  const [method, setMethod] = useState<AppointmentMethod>('FaceToFace');
  const [hostId, setHostId] = useState(manifest?.hosts[0]?.id ?? '');
  const [categoryId, setCategoryId] = useState(manifest?.categories[0]?.id ?? '');
  const [description, setDescription] = useState('');
  const [seriesEnabled, setSeriesEnabled] = useState(false);
  const [frequency, setFrequency] = useState<SeriesFrequency>('Weekly');
  const [count, setCount] = useState('4');
  const [plan, setPlan] = useState<SeriesPlan | null>(null);
  const [busy, setBusy] = useState(false);

  const hosts = manifest?.hosts ?? [];
  const categories = manifest?.categories.filter((x) => x.isActive) ?? [];

  const input = useMemo<AppointmentInput | null>(() => {
    if (!firstName.trim() || !lastName.trim() || !phone.trim() || !hostId || !categoryId) return null;

    const durationMinutes = Number(duration);
    if (!Number.isFinite(durationMinutes) || durationMinutes < 5 || durationMinutes > 240) return null;

    return {
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      phone: phone.trim(),
      startsAtLocal: `${date}T${time}:00`,
      durationMinutes,
      hostUserId: hostId,
      categoryId,
      method,
      description: description.trim() ? description.trim() : null,
      studentFirstName: null,
      studentLastName: null,
      reminderOffsetsMinutes: [30],
    };
  }, [firstName, lastName, phone, date, time, duration, hostId, categoryId, method, description]);

  const preview = async () => {
    if (!input || !online) {
      Alert.alert('Önizleme', 'Önizleme için bağlantı ve zorunlu alanlar gerekir.');
      return;
    }

    setBusy(true);
    try {
      setPlan(await api.previewSeries(input, { frequency, count: Number(count) }));
    } catch (error) {
      Alert.alert('Hata', error instanceof Error ? error.message : 'Seri önizlenemedi.');
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    if (!input) {
      Alert.alert('Eksik bilgi', 'Ad, soyad, telefon, görüşmeci ve kategori zorunludur.');
      return;
    }

    if (seriesEnabled) {
      if (!online) {
        Alert.alert('Seri', 'Seri oluşturmak için bağlantı gerekir.');
        return;
      }

      setBusy(true);
      try {
        const result = await api.createSeries(input, { frequency, count: Number(count) });
        if (!result.success) {
          Alert.alert('Seri', result.error ?? 'Seri oluşturulamadı.');
          return;
        }

        Alert.alert('Seri oluşturuldu', result.summary);
        onSaved();
      } catch (error) {
        Alert.alert('Hata', error instanceof Error ? error.message : 'Seri oluşturulamadı.');
      } finally {
        setBusy(false);
      }
      return;
    }

    if (!online) {
      await enqueue('appointment.create', input);
      Alert.alert('Kuyruğa alındı', 'Bağlantı gelince görüşme sunucuya gönderilecek.');
      onSaved();
      return;
    }

    setBusy(true);
    try {
      const result = await api.createAppointment(input);
      if (!result.success) {
        Alert.alert('Görüşme', result.error ?? 'Görüşme oluşturulamadı.');
        return;
      }

      await flushOutbox();
      Alert.alert('Kaydedildi', 'Görüşme oluşturuldu.');
      onSaved();
    } catch (error) {
      Alert.alert('Hata', error instanceof Error ? error.message : 'Görüşme oluşturulamadı.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={onBack} style={styles.back}>
          <Text style={styles.backText}>‹ Geri</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Yeni görüşme</Text>
      </View>

      <ScrollView contentContainerStyle={styles.body}>
        {!online ? (
          <View style={styles.offlineBar}>
            <Text style={styles.offlineText}>
              Çevrimdışı · Kayıt kuyruğa alınır (seri hariç)
            </Text>
          </View>
        ) : null}

        <Field label="Ad" value={firstName} onChange={setFirstName} />
        <Field label="Soyad" value={lastName} onChange={setLastName} />
        <Field label="Telefon (05xx...)" value={phone} onChange={setPhone} keyboard="phone-pad" />

        <View style={styles.row}>
          <View style={{ flex: 1, marginRight: spacing.sm }}>
            <Field label="Tarih (YYYY-AA-GG)" value={date} onChange={setDate} />
          </View>
          <View style={{ width: 100 }}>
            <Field label="Saat (SS:dd)" value={time} onChange={setTime} />
          </View>
        </View>

        <Field label="Süre (dk)" value={duration} onChange={setDuration} keyboard="number-pad" />
        <Field label="Açıklama" value={description} onChange={setDescription} multiline />

        <Text style={styles.label}>Görüşmeci</Text>
        <View style={styles.chips}>
          {hosts.map((host) => (
            <Chip
              key={host.id}
              label={host.displayName}
              selected={host.id === hostId}
              onPress={() => setHostId(host.id)}
            />
          ))}
        </View>

        <Text style={styles.label}>Kategori</Text>
        <View style={styles.chips}>
          {categories.map((category) => (
            <Chip
              key={category.id}
              label={category.name}
              color={category.colorHex}
              selected={category.id === categoryId}
              onPress={() => setCategoryId(category.id)}
            />
          ))}
        </View>

        <Text style={styles.label}>Yöntem</Text>
        <View style={styles.chips}>
          {methods.map((item) => (
            <Chip
              key={item.value}
              label={item.label}
              selected={item.value === method}
              onPress={() => setMethod(item.value)}
            />
          ))}
        </View>

        <View style={styles.seriesRow}>
          <Text style={styles.label}>Tekrarlayan seri</Text>
          <Switch value={seriesEnabled} onValueChange={setSeriesEnabled} />
        </View>

        {seriesEnabled ? (
          <View>
            <Text style={styles.label}>Sıklık</Text>
            <View style={styles.chips}>
              {frequencies.map((item) => (
                <Chip
                  key={item.value}
                  label={item.label}
                  selected={item.value === frequency}
                  onPress={() => setFrequency(item.value)}
                />
              ))}
            </View>
            <Field label="Kayıt sayısı (1-12)" value={count} onChange={setCount} keyboard="number-pad" />
            <TouchableOpacity style={styles.secondary} onPress={() => void preview()} disabled={busy}>
              <Text style={styles.secondaryText}>Önizle</Text>
            </TouchableOpacity>
            {plan ? (
              <View style={styles.plan}>
                <Text style={styles.planSummary}>{plan.summary}</Text>
                {plan.slots.map((slot) => (
                  <Text key={slot.index} style={styles.planRow}>
                    {slot.positionText} · {slot.dateText} {slot.timeText} · {slot.stateText}
                  </Text>
                ))}
              </View>
            ) : null}
          </View>
        ) : null}

        <TouchableOpacity style={styles.primary} onPress={() => void save()} disabled={busy}>
          <Text style={styles.primaryText}>{busy ? 'Kaydediliyor…' : 'Kaydet'}</Text>
        </TouchableOpacity>
      </ScrollView>
    </View>
  );
}

type FieldProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
  keyboard?: 'default' | 'phone-pad' | 'number-pad';
  multiline?: boolean;
};

function Field({ label, value, onChange, keyboard = 'default', multiline }: FieldProps) {
  return (
    <View style={{ marginTop: spacing.sm }}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChange}
        keyboardType={keyboard}
        multiline={multiline}
        style={[styles.input, multiline ? { height: 70, textAlignVertical: 'top' } : null]}
        autoCapitalize="none"
      />
    </View>
  );
}

function Chip({
  label,
  selected,
  onPress,
  color,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
  color?: string;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      style={[styles.chip, selected ? styles.chipSelected : null]}
    >
      {color ? <View style={[styles.dot, { backgroundColor: color }]} /> : null}
      <Text style={[styles.chipText, selected ? styles.chipTextSelected : null]}>{label}</Text>
    </TouchableOpacity>
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
  body: { padding: spacing.md, paddingBottom: spacing.xl },
  offlineBar: { backgroundColor: '#FBF3E2', borderRadius: radius.md, padding: spacing.sm },
  offlineText: { color: colors.offline, fontSize: 12 },
  label: { fontSize: 11, fontWeight: '700', color: colors.textMuted, marginTop: spacing.sm },
  input: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 14,
    color: colors.text,
    marginTop: 4,
  },
  row: { flexDirection: 'row' },
  chips: { flexDirection: 'row', flexWrap: 'wrap', marginTop: spacing.xs },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: 6,
    marginRight: spacing.sm,
    marginBottom: spacing.sm,
  },
  chipSelected: { backgroundColor: colors.primary, borderColor: colors.primary },
  chipText: { fontSize: 12, color: colors.text },
  chipTextSelected: { color: '#FFFFFF', fontWeight: '600' },
  dot: { width: 8, height: 8, borderRadius: 4, marginRight: 6 },
  seriesRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: spacing.md,
  },
  secondary: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  secondaryText: { color: colors.primary, fontWeight: '700' },
  plan: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  planSummary: { fontWeight: '600', color: colors.text, marginBottom: 4 },
  planRow: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  primary: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    marginTop: spacing.lg,
  },
  primaryText: { color: '#FFFFFF', fontWeight: '700' },
});
