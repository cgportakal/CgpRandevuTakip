import React, { useEffect, useMemo, useState } from 'react';
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
import { api, ApiError } from '../api/client';
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
  const [roomId, setRoomId] = useState('');
  const [participantIds, setParticipantIds] = useState<string[]>([]);
  const [guests, setGuests] = useState<{ firstName: string; lastName: string; phone: string }[]>([]);
  const [categoryId, setCategoryId] = useState(manifest?.categories[0]?.id ?? '');
  const [description, setDescription] = useState('');
  const [seriesEnabled, setSeriesEnabled] = useState(false);
  const [frequency, setFrequency] = useState<SeriesFrequency>('Weekly');
  const [count, setCount] = useState('4');
  const [plan, setPlan] = useState<SeriesPlan | null>(null);
  const [busy, setBusy] = useState(false);

  const hosts = manifest?.hosts ?? [];
  const categories = manifest?.categories.filter((x) => x.isActive) ?? [];
  const rooms = (manifest?.rooms ?? []).filter(
    (room) => room.kind === 'Shared' || room.hostUserId === hostId,
  );

  useEffect(() => {
    if (roomId) return;
    const room = manifest?.rooms.find(
      (item) => item.kind === 'HostRoom' && item.hostUserId === hostId,
    );
    if (room) setRoomId(room.id);
  }, [manifest, hostId, roomId]);

  const selectHost = (value: string) => {
    setHostId(value);
    setParticipantIds((current) => current.filter((id) => id !== value));
    const room = manifest?.rooms.find(
      (item) => item.kind === 'HostRoom' && item.hostUserId === value,
    );
    setRoomId(room?.id ?? '');
  };

  const selectMethod = (value: AppointmentMethod) => {
    setMethod(value);
    if (value !== 'FaceToFace') {
      setRoomId('');
    } else {
      const room = manifest?.rooms.find(
        (item) => item.kind === 'HostRoom' && item.hostUserId === hostId,
      );
      setRoomId(room?.id ?? '');
    }
  };

  const addGuest = () =>
    setGuests((current) => [...current, { firstName: '', lastName: '', phone: '' }]);

  const input = useMemo<AppointmentInput | null>(() => {
    if (!firstName.trim() || !lastName.trim() || !phone.trim() || !hostId || !categoryId) return null;

    const durationMinutes = Number(duration);
    if (!Number.isFinite(durationMinutes) || durationMinutes < 5 || durationMinutes > 240) return null;
    if (method === 'FaceToFace' && !roomId) return null;

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
      roomId: roomId || null,
      participantHostUserIds: participantIds.filter((id) => id !== hostId),
      guests: [
        {
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          phone: phone.trim(),
          isPrimary: true,
        },
        ...guests.map((guest) => ({
          firstName: guest.firstName.trim(),
          lastName: guest.lastName.trim(),
          phone: guest.phone.trim(),
          isPrimary: false,
        })),
      ],
    };
  }, [
    firstName,
    lastName,
    phone,
    date,
    time,
    duration,
    hostId,
    categoryId,
    method,
    description,
    roomId,
    participantIds,
    guests,
  ]);

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

  const save = async (force = false) => {
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
      // Çevrimdışıyken çakışma denetimi yapılamaz; kullanıcı kaydı bilerek
      // oluşturduğu için kuyruktan gönderilirken çakışma engeli atlanır.
      await enqueue('appointment.create', { ...input, ignoreConflicts: true });
      Alert.alert('Kuyruğa alındı', 'Bağlantı gelince görüşme sunucuya gönderilecek.');
      onSaved();
      return;
    }

    setBusy(true);
    try {
      const payload = force ? { ...input, ignoreConflicts: true } : input;
      const result = await api.createAppointment(payload);
      if (!result.success) {
        if (result.conflicts?.length) {
          Alert.alert(
            'Çakışma',
            result.error ?? 'Bu saatte çakışan bir görüşme var.',
            [
              { text: 'Vazgeç', style: 'cancel' },
              { text: 'Yine de kaydet', onPress: () => void save(true) },
            ],
          );
          return;
        }

        Alert.alert('Görüşme', result.error ?? 'Görüşme oluşturulamadı.');
        return;
      }

      await flushOutbox();
      Alert.alert('Kaydedildi', 'Görüşme oluşturuldu.');
      onSaved();
    } catch (error) {
      if (error instanceof ApiError) {
        const payload = error.payload as
          | { error?: string; conflicts?: unknown[] }
          | undefined;

        if (payload?.conflicts?.length) {
          Alert.alert(
            'Çakışma',
            payload.error ?? 'Bu saatte çakışan bir görüşme var.',
            [
              { text: 'Vazgeç', style: 'cancel' },
              { text: 'Yine de kaydet', onPress: () => void save(true) },
            ],
          );
          return;
        }
      }

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
              Çevrimdışı · Kayıt kuyruğa alınır; çakışma denetimi atlanır (seri hariç)
            </Text>
          </View>
        ) : null}

        <Field label="Ad" value={firstName} onChange={setFirstName} testID="first-name" />
        <Field label="Soyad" value={lastName} onChange={setLastName} testID="last-name" />
        <Field label="Telefon (05xx...)" value={phone} onChange={setPhone} keyboard="phone-pad" testID="phone" />

        <View style={styles.row}>
          <View style={{ flex: 1, marginRight: spacing.sm }}>
            <Field label="Tarih (YYYY-AA-GG)" value={date} onChange={setDate} testID="date" />
          </View>
          <View style={{ width: 100 }}>
            <Field label="Saat (SS:dd)" value={time} onChange={setTime} testID="time" />
          </View>
        </View>

        <Field label="Süre (dk)" value={duration} onChange={setDuration} keyboard="number-pad" testID="duration" />
        <Field label="Açıklama" value={description} onChange={setDescription} multiline />

        <Text style={styles.label}>Görüşmeci</Text>
        <View style={styles.chips}>
          {hosts.map((host) => (
            <Chip
              key={host.id}
              label={host.displayNameWithTitle}
              selected={host.id === hostId}
              onPress={() => selectHost(host.id)}
            />
          ))}
        </View>

        <Text style={styles.label}>Oda</Text>
        <View style={styles.chips}>
          {rooms.map((room) => (
            <Chip
              key={room.id}
              label={room.displayName}
              selected={room.id === roomId}
              onPress={() => setRoomId(room.id)}
            />
          ))}
        </View>

        <Text style={styles.label}>Katılımcılar</Text>
        <View style={styles.chips}>
          {hosts
            .filter((host) => host.id !== hostId)
            .map((host) => (
              <Chip
                key={host.id}
                label={host.displayNameWithTitle}
                selected={participantIds.includes(host.id)}
                onPress={() =>
                  setParticipantIds((current) =>
                    current.includes(host.id)
                      ? current.filter((id) => id !== host.id)
                      : [...current, host.id],
                  )
                }
              />
            ))}
        </View>

        <Text style={styles.label}>Ek misafirler</Text>
        {guests.map((guest, index) => (
          <View key={index} style={styles.row}>
            <View style={{ flex: 1, marginRight: spacing.sm }}>
              <TextInput
                testID={`guest-first-${index}`}
                style={styles.input}
                placeholder="Ad"
                value={guest.firstName}
                onChangeText={(value) =>
                  setGuests((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index ? { ...item, firstName: value } : item,
                    ),
                  )
                }
              />
            </View>
            <View style={{ flex: 1, marginRight: spacing.sm }}>
              <TextInput
                testID={`guest-last-${index}`}
                style={styles.input}
                placeholder="Soyad"
                value={guest.lastName}
                onChangeText={(value) =>
                  setGuests((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index ? { ...item, lastName: value } : item,
                    ),
                  )
                }
              />
            </View>
            <View style={{ flex: 1, marginRight: spacing.sm }}>
              <TextInput
                testID={`guest-phone-${index}`}
                style={styles.input}
                placeholder="Telefon"
                keyboardType="phone-pad"
                value={guest.phone}
                onChangeText={(value) =>
                  setGuests((current) =>
                    current.map((item, itemIndex) =>
                      itemIndex === index ? { ...item, phone: value } : item,
                    ),
                  )
                }
              />
            </View>
            <TouchableOpacity
              onPress={() => setGuests((current) => current.filter((_, itemIndex) => itemIndex !== index))}
            >
              <Text style={styles.removeGuest}>Sil</Text>
            </TouchableOpacity>
          </View>
        ))}
        <TouchableOpacity testID="add-guest" style={styles.addGuest} onPress={addGuest}>
          <Text style={styles.addGuestText}>+ Misafir ekle</Text>
        </TouchableOpacity>

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
              onPress={() => selectMethod(item.value)}
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

        <TouchableOpacity testID="save-appointment" style={styles.primary} onPress={() => void save()} disabled={busy}>
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
  testID?: string;
};

function Field({ label, value, onChange, keyboard = 'default', multiline, testID }: FieldProps) {
  return (
    <View style={{ marginTop: spacing.sm }}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        testID={testID}
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
  row: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.xs },
  removeGuest: { color: colors.danger, fontWeight: '700', paddingHorizontal: spacing.xs },
  addGuest: { paddingVertical: spacing.sm },
  addGuestText: { color: colors.primary, fontWeight: '700' },
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
