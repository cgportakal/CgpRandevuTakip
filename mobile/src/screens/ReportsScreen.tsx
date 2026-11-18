import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useNetInfo } from '@react-native-community/netinfo';
import { api } from '../api/client';
import type { ReportKind, ReportResult } from '../api/types';
import { colors, radius, spacing } from '../theme';

type Props = {
  onBack: () => void;
};

const kinds: { value: ReportKind; label: string }[] = [
  { value: 'Summary', label: 'Özet' },
  { value: 'HostPerformance', label: 'Görüşmeci' },
  { value: 'CategoryTrend', label: 'Kategori' },
  { value: 'Attendance', label: 'Katılım' },
  { value: 'Repeat', label: 'Tekrar' },
  { value: 'PersonHistory', label: 'Kişi geçmişi' },
  { value: 'AuditActivity', label: 'Denetim' },
];

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function key(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function ReportsScreen({ onBack }: Props) {
  const netInfo = useNetInfo();
  const online = netInfo.isConnected !== false;

  const [kind, setKind] = useState<ReportKind>('Summary');
  const [result, setResult] = useState<ReportResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [personSearch, setPersonSearch] = useState('');

  const load = useCallback(
    async (target: ReportKind, search?: string) => {
      if (!online) {
        Alert.alert('Raporlar', 'Raporlar için bağlantı gerekir.');
        return;
      }

      const today = new Date();
      const from = new Date(today.getFullYear(), today.getMonth(), 1);

      setLoading(true);
      try {
        setResult(await api.getReport(target, key(from), key(today), search));
      } catch (error) {
        Alert.alert('Hata', error instanceof Error ? error.message : 'Rapor yüklenemedi.');
      } finally {
        setLoading(false);
      }
    },
    [online],
  );

  useEffect(() => {
    void load('Summary');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const selectKind = (value: ReportKind) => {
    setKind(value);
    setResult(null);
    void load(value, value === 'PersonHistory' ? personSearch : undefined);
  };

  const summary = result?.summary;

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={onBack} style={styles.back}>
          <Text style={styles.backText}>‹ Geri</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Raporlar</Text>
      </View>

      <ScrollView contentContainerStyle={styles.body}>
        <Text style={styles.label}>Rapor türü</Text>
        <View style={styles.chips}>
          {kinds.map((item) => (
            <TouchableOpacity
              key={item.value}
              onPress={() => selectKind(item.value)}
              style={[styles.chip, item.value === kind ? styles.chipSelected : null]}
            >
              <Text style={[styles.chipText, item.value === kind ? styles.chipTextSelected : null]}>
                {item.label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        {kind === 'PersonHistory' ? (
          <View style={styles.searchRow}>
            <TextInput
              value={personSearch}
              onChangeText={setPersonSearch}
              placeholder="Ad veya telefon"
              style={[styles.input, { flex: 1 }]}
            />
            <TouchableOpacity
              style={styles.searchButton}
              onPress={() => void load('PersonHistory', personSearch)}
            >
              <Text style={styles.searchText}>Ara</Text>
            </TouchableOpacity>
          </View>
        ) : null}

        {loading ? <ActivityIndicator color={colors.primary} style={{ marginTop: spacing.lg }} /> : null}

        {summary ? (
          <View style={styles.card}>
            <Text style={styles.range}>{summary.rangeText}</Text>
            <Text style={styles.kpi}>{summary.total} görüşme</Text>
            <Text style={styles.meta}>
              Tamamlanma {summary.completionRateText} · Gelmedi {summary.noShowRateText} · İptal{' '}
              {summary.cancelRateText}
            </Text>
            <Text style={styles.meta}>
              Ortalama {summary.averageDurationText} · {summary.uniquePersons} kişi
            </Text>
          </View>
        ) : null}

        {result?.hostPerformance.map((item) => (
          <View key={item.hostName} style={styles.row}>
            <Text style={styles.rowTitle}>{item.hostName}</Text>
            <Text style={styles.meta}>
              {item.total} görüşme · {item.completionRateText} tamam · {item.noShowRateText} gelmedi
            </Text>
          </View>
        ))}

        {result?.categoryTrends?.map((item) => (
          <View key={item.categoryId} style={styles.row}>
            <View style={styles.rowHeading}>
              <View style={[styles.colorDot, { backgroundColor: item.colorHex }]} />
              <Text style={styles.rowTitle}>{item.categoryName}</Text>
            </View>
            <Text style={styles.meta}>{item.total} görüşme</Text>
          </View>
        ))}

        {result?.attendance?.map((item) => (
          <View key={item.dateText} style={styles.row}>
            <Text style={styles.rowTitle}>
              {item.weekdayText} · {item.dateText}
            </Text>
            <Text style={styles.meta}>
              {item.total} toplam · {item.arrived} geldi · {item.completed} tamam · {item.noShow}{' '}
              gelmedi
            </Text>
          </View>
        ))}

        {result?.repeatPersons.map((item) => (
          <View key={`${item.personName}-${item.phone}`} style={styles.row}>
            <Text style={styles.rowTitle}>{item.personName}</Text>
            <Text style={styles.meta}>
              {item.visitCount} görüşme · son {item.lastVisitText} · {item.intervalText}
            </Text>
          </View>
        ))}

        {result?.personHistory.map((person) => (
          <View key={`${person.personName}-${person.phone}`} style={styles.card}>
            <Text style={styles.rowTitle}>{person.personName}</Text>
            <Text style={styles.meta}>{person.summaryText} · {person.phone}</Text>
            {person.entries.map((entry, index) => (
              <Text key={`${entry.dateText}-${index}`} style={styles.entry}>
                {entry.dateText} · {entry.hostName} · {entry.statusText}
              </Text>
            ))}
          </View>
        ))}

        {result?.auditActivity ? (
          <View style={styles.card}>
            <Text style={styles.rowTitle}>{result.auditActivity.total} kayıt</Text>
            {result.auditActivity.actions.map((action) => (
              <Text key={action.actionName} style={styles.entry}>
                {action.actionName}: {action.count}
              </Text>
            ))}
          </View>
        ) : null}
      </ScrollView>
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
  body: { padding: spacing.md, paddingBottom: spacing.xl },
  label: { fontSize: 11, fontWeight: '700', color: colors.textMuted },
  chips: { flexDirection: 'row', flexWrap: 'wrap', marginTop: spacing.sm },
  chip: {
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
  searchRow: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.sm },
  input: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 14,
    color: colors.text,
  },
  searchButton: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    marginLeft: spacing.sm,
  },
  searchText: { color: '#FFFFFF', fontWeight: '700' },
  card: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginTop: spacing.md,
  },
  range: { fontSize: 12, color: colors.textMuted },
  kpi: { fontSize: 24, fontWeight: '700', color: colors.text, marginTop: 4 },
  meta: { fontSize: 12, color: colors.textMuted, marginTop: 4 },
  row: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  rowTitle: { fontSize: 14, fontWeight: '700', color: colors.text },
  rowHeading: { flexDirection: 'row', alignItems: 'center' },
  colorDot: { width: 10, height: 10, borderRadius: 5, marginRight: spacing.sm },
  entry: { fontSize: 12, color: colors.textMuted, marginTop: 4 },
});
