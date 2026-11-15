export type UserRole = 'Admin' | 'Secretary' | 'Host';

export type User = {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
};

export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  user: User;
};

export type Category = {
  id: string;
  name: string;
  colorHex: string;
  sortOrder: number;
  isActive: boolean;
  usageCount: number;
};

export type Host = {
  id: string;
  displayName: string;
  username: string;
  role: UserRole;
  isLoginEnabled: boolean;
  isActive: boolean;
  appointmentCount: number;
};

export type AppointmentStatus =
  | 'Scheduled'
  | 'Arrived'
  | 'NoShow'
  | 'Postponed'
  | 'Cancelled'
  | 'Completed';

export type AppointmentMethod = 'Phone' | 'FaceToFace' | 'WhatsApp' | 'Online';

export type AppointmentListItem = {
  id: string;
  startsAtLocal: string;
  endsAtLocal: string;
  durationMinutes: number;
  personFirstName: string;
  personLastName: string;
  phoneE164: string;
  hostUserId: string;
  hostName: string;
  categoryId: string;
  categoryName: string;
  categoryColorHex: string;
  method: AppointmentMethod;
  status: AppointmentStatus;
  isOverdue: boolean;
  isDeleted: boolean;
  studentFullName: string | null;
  personFullName: string;
  timeLabel: string;
  endTimeLabel: string;
  methodText: string;
  statusText: string;
};

export type Institution = {
  name: string;
  email: string;
  phone: string;
  address: string;
  logoUrl: string | null;
};

export type PublicSettings = {
  theme: string;
  defaultReminderMinutes: number;
  workHoursStart: string;
  workHoursEnd: string;
  allowOutOfHoursAppointments: boolean;
  notificationsEnabled: boolean;
  pushEnabled: boolean;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  dailyShareTime: string;
};

export type VersionPolicy = {
  minDesktopVersion: string;
  minMobileVersion: string;
  minWebVersion: string;
  latestDesktopVersion: string;
  latestMobileVersion: string;
};

export type ContentManifest = {
  version: string;
  generatedAtUtc: string;
  institution: Institution;
  settings: PublicSettings;
  categories: Category[];
  hosts: Host[];
  features: Record<string, boolean>;
  versionPolicy: VersionPolicy;
};

export type AppointmentOutcomeInput = {
  attendance: 'Arrived' | 'NoShow' | 'Postponed' | 'Cancelled';
  summary: string | null;
  willRepeat: boolean;
  postNotes: string | null;
  isClosed: boolean;
};
