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

export type AppointmentInput = {
  firstName: string;
  lastName: string;
  phone: string;
  startsAtLocal: string;
  durationMinutes: number;
  hostUserId: string;
  categoryId: string;
  method: AppointmentMethod;
  description: string | null;
  studentFirstName: string | null;
  studentLastName: string | null;
  reminderOffsetsMinutes: number[];
  ignoreConflicts?: boolean;
};

export type AppointmentDetail = {
  id: string;
  startsAtLocal: string;
  endsAtLocal: string;
  durationMinutes: number;
  personFullName: string;
  phoneE164: string;
  hostUserId: string;
  hostName: string;
  categoryId: string;
  categoryName: string;
  categoryColorHex: string;
  method: AppointmentMethod;
  status: AppointmentStatus;
  isClosed: boolean;
  description: string | null;
  studentFullName: string;
  remindersText: string;
  seriesId: string | null;
  seriesIndex: number | null;
  seriesCount: number | null;
  dateLabel: string;
  timeRangeLabel: string;
  statusText: string;
};

export type SeriesFrequency = 'Weekly' | 'Biweekly' | 'Monthly';

export type SeriesRequest = {
  frequency: SeriesFrequency;
  count: number;
};

export type SeriesSlot = {
  index: number;
  startsAtLocal: string;
  endsAtLocal: string;
  state: 'Create' | 'Conflict' | 'Past';
  note: string | null;
  dateText: string;
  timeText: string;
  positionText: string;
  stateText: string;
  willCreate: boolean;
};

export type SeriesPlan = {
  slots: SeriesSlot[];
  error: string | null;
  createCount: number;
  skippedCount: number;
  canSave: boolean;
  summary: string;
};

export type SeriesCreateResult = {
  success: boolean;
  error: string | null;
  seriesId: string | null;
  createdCount: number;
  skippedCount: number;
  summary: string;
};

export type SeriesCancelResult = {
  success: boolean;
  error: string | null;
  cancelledCount: number;
  skippedCount: number;
  summary: string;
};

export type ReportSummary = {
  fromLocalDate: string;
  toLocalDate: string;
  total: number;
  completed: number;
  arrived: number;
  noShow: number;
  cancelled: number;
  postponed: number;
  scheduled: number;
  uniquePersons: number;
  returningPersons: number;
  repeatRequested: number;
  averageDurationMinutes: number;
  totalMinutes: number;
  granularity: 'Daily' | 'Monthly';
  points: { label: string; total: number; completed: number; noShow: number }[];
  categories: { categoryId: string; categoryName: string; colorHex: string; total: number }[];
  hosts: { hostUserId: string; hostName: string; total: number; completed: number; cancelled: number }[];
  hours: { hour: number; label: string; total: number }[];
  isEmpty: boolean;
  rangeText: string;
  completionRateText: string;
  noShowRateText: string;
  cancelRateText: string;
  averageDurationText: string;
  bucketLabel: string;
};

export type ReportKind =
  | 'Summary'
  | 'HostPerformance'
  | 'CategoryTrend'
  | 'Attendance'
  | 'Repeat'
  | 'PersonHistory'
  | 'AuditActivity';

export type ReportResult = {
  kind: ReportKind;
  summary: ReportSummary;
  hostPerformance: {
    hostName: string;
    total: number;
    completed: number;
    noShow: number;
    cancelled: number;
    completionRateText: string;
    noShowRateText: string;
    averageDurationText: string;
  }[];
  repeatPersons: {
    personName: string;
    phone: string;
    visitCount: number;
    lastVisitText: string;
    intervalText: string;
  }[];
  personHistory: {
    personName: string;
    phone: string;
    summaryText: string;
    entries: { dateText: string; hostName: string; categoryName: string; statusText: string }[];
  }[];
  auditActivity: {
    total: number;
    actions: { actionName: string; count: number }[];
    entries: { createdAtText: string; userName: string; actionName: string; entityText: string }[];
  } | null;
};
