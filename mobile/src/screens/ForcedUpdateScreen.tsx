import React from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { colors, radius, spacing } from '../theme';

type Props = {
  currentVersion: string;
  requiredVersion: string;
  latestVersion?: string | null;
  onRetry: () => void;
};

export function ForcedUpdateScreen({
  currentVersion,
  requiredVersion,
  latestVersion,
  onRetry,
}: Props) {
  return (
    <View style={styles.container}>
      <View style={styles.card}>
        <Text style={styles.brand}>CGP</Text>
        <Text style={styles.title}>GÃ¼ncelleme gerekli</Text>
        <Text style={styles.subtitle}>
          Bu uygulama sÃ¼rÃ¼mÃ¼ artÄ±k desteklenmiyor. Devam etmek iÃ§in uygulamayÄ±
          maÄŸazadan veya EAS gÃ¼ncellemesiyle gÃ¼ncelleyin.
        </Text>

        <View style={styles.versions}>
          <Row label="Mevcut sÃ¼rÃ¼m" value={currentVersion} />
          <Row label="Gerekli sÃ¼rÃ¼m" value={requiredVersion} />
          {latestVersion ? <Row label="Son sÃ¼rÃ¼m" value={latestVersion} /> : null}
        </View>

        <TouchableOpacity style={styles.button} onPress={onRetry}>
          <Text style={styles.buttonText}>Yeniden dene</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
  },
  brand: {
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: 2,
    color: colors.primary,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text,
    marginTop: spacing.sm,
  },
  subtitle: {
    fontSize: 14,
    lineHeight: 21,
    color: colors.textMuted,
    marginTop: spacing.sm,
  },
  versions: {
    marginTop: spacing.md,
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.background,
    gap: 8,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 12,
  },
  rowLabel: {
    fontSize: 13,
    color: colors.textMuted,
  },
  rowValue: {
    fontSize: 13,
    fontWeight: '700',
    color: colors.text,
  },
  button: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: 12,
    alignItems: 'center',
    marginTop: spacing.lg,
  },
  buttonText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
});

