export function toDateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function todayKey(): string {
  return toDateKey(new Date());
}

export function shiftDateKey(key: string, offset: number): string {
  const date = new Date(`${key}T12:00:00`);
  date.setDate(date.getDate() + offset);
  return toDateKey(date);
}

const months = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];

const weekDays = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];

export function formatLongDate(key: string): string {
  const [year, month, day] = key.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  return `${day} ${months[month - 1]} ${year}, ${weekDays[date.getDay()]}`;
}

export function formatShortDate(key: string): string {
  const [year, month, day] = key.split('-');
  return `${day}.${month}.${year}`;
}

export function formatTime(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
}

export function firstNameMonthRange(): { from: string; to: string } {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth(), 1);
  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0);
  return { from: toDateKey(from), to: toDateKey(to) };
}

export function lastMonthRange(): { from: string; to: string } {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const to = new Date(now.getFullYear(), now.getMonth(), 0);
  return { from: toDateKey(from), to: toDateKey(to) };
}

export function lastDaysRange(days: number): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - (days - 1));
  return { from: toDateKey(from), to: toDateKey(to) };
}

export function thisYearRange(): { from: string; to: string } {
  const now = new Date();
  return {
    from: toDateKey(new Date(now.getFullYear(), 0, 1)),
    to: toDateKey(new Date(now.getFullYear(), 11, 31)),
  };
}

export function minutesToLabel(minutes: number): string {
  if (minutes >= 1440) return `${Math.round(minutes / 1440)} gün`;
  if (minutes >= 60) return `${Math.round(minutes / 60)} saat`;
  return `${minutes} dk`;
}
