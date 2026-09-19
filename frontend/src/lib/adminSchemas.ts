import { z } from 'zod'

/**
 * Admin API Zod schemas. Every E10 admin response is validated at the
 * transport boundary; backend contract drift is surfaced immediately rather
 * than silently corrupting operational UI.
 */

/* -------------------------------------------------------------------------- */
/*  Provider Dashboard                                                       */
/* -------------------------------------------------------------------------- */

const fetchModeSchema = z.enum(['recent', 'full'])

// The API uses a global `WhenWritingNull` JSON policy, so nullable record
// properties are omitted rather than serialized as explicit JSON nulls. Keep
// the application model consistently nullable while accepting either wire
// representation.
const omittedNullableStringSchema = z
  .string()
  .nullish()
  .transform((value) => value ?? null)
const omittedNullableIntegerSchema = z
  .number()
  .int()
  .nullish()
  .transform((value) => value ?? null)

export const adminProviderDashboardItemSchema = z.object({
  id: z.number().int(),
  name: z.string(),
  baseUrl: z.string(),
  isEnabled: z.boolean(),
  intervalMinutes: z.number().int(),
  lastFetchStatus: omittedNullableStringSchema,
  lastFetchAt: omittedNullableStringSchema,
  nextFetchTime: omittedNullableStringSchema,
  supportedModes: z.array(fetchModeSchema),
  defaultMode: fetchModeSchema,
  recentCooldownMinutes: omittedNullableIntegerSchema,
  fullCooldownMinutes: omittedNullableIntegerSchema,
  minimumIntervalMinutes: z.number().int(),
  requiresApiKey: z.boolean().default(false),
  credentialConfigured: z.boolean().default(false),
  credentialKeyVersion: omittedNullableIntegerSchema,
  credentialUpdatedAt: omittedNullableStringSchema,
})

export type AdminProviderDashboardItem = z.infer<
  typeof adminProviderDashboardItemSchema
>

export const adminProviderListSchema = z.array(adminProviderDashboardItemSchema)

/* -------------------------------------------------------------------------- */
/*  Fetch Queued / Job Status                                                */
/* -------------------------------------------------------------------------- */

export const fetchQueuedResponseSchema = z.object({
  jobId: z.string().uuid(),
  status: z.literal('queued'),
})

export type FetchQueuedResponse = z.infer<typeof fetchQueuedResponseSchema>

const jobStatusValues = ['queued', 'running', 'success', 'failed'] as const

export const fetchJobStatusSchema = z.enum(jobStatusValues)

export type FetchJobStatus = z.infer<typeof fetchJobStatusSchema>

export const adminFetchHistoryItemSchema = z.object({
  jobId: z.string().uuid(),
  providerId: z.number().int(),
  status: fetchJobStatusSchema,
  trigger: z.enum(['scheduled', 'manual']),
  mode: fetchModeSchema,
  startedAt: z.string(),
  finishedAt: omittedNullableStringSchema,
  recordsAdded: z.number().int(),
  recordsUpdated: z.number().int(),
  errorDetail: omittedNullableStringSchema,
})

export type AdminFetchHistoryItem = z.infer<typeof adminFetchHistoryItemSchema>

/* -------------------------------------------------------------------------- */
/*  Provider History (paged)                                                 */
/* -------------------------------------------------------------------------- */

export const adminProviderHistoryPageSchema = z.object({
  items: z.array(adminFetchHistoryItemSchema),
  page: z.number().int(),
  pageSize: z.number().int(),
  totalCount: z.number().int(),
})

export type AdminProviderHistoryPage = z.infer<
  typeof adminProviderHistoryPageSchema
>

/* -------------------------------------------------------------------------- */
/*  Database Summary                                                         */
/* -------------------------------------------------------------------------- */

export const adminSummarySchema = z.object({
  total: z.number().int(),
  ipv4: z.number().int(),
  ipv6: z.number().int(),
  domain: z.number().int(),
  url: z.number().int(),
})

export type AdminSummary = z.infer<typeof adminSummarySchema>

/* -------------------------------------------------------------------------- */
/*  Structured Logs                                                          */
/* -------------------------------------------------------------------------- */

export const adminLogEntrySchema = z.object({
  timestamp: z.string(),
  level: z.string(),
  correlationId: omittedNullableStringSchema,
  message: z.string(),
  exception: omittedNullableStringSchema,
})

export type AdminLogEntry = z.infer<typeof adminLogEntrySchema>

export const adminLogListSchema = z.array(adminLogEntrySchema)

export const adminLogPageSchema = z.object({
  items: z.array(adminLogEntrySchema),
  page: z.number().int(),
  pageSize: z.number().int(),
  totalCount: z.number().int(),
})

export type AdminLogPage = z.infer<typeof adminLogPageSchema>

/* -------------------------------------------------------------------------- */
/*  Settings (read + write)                                                  */
/* -------------------------------------------------------------------------- */

export const providerSettingResponseSchema = z.object({
  id: z.number().int(),
  name: z.string(),
  isEnabled: z.boolean(),
  intervalMinutes: z.number().int(),
  minimumIntervalMinutes: z.number().int(),
  requiresApiKey: z.boolean().default(false),
})

export type ProviderSettingResponse = z.infer<
  typeof providerSettingResponseSchema
>

export const settingsResponseSchema = z.object({
  logLevel: z.string(),
  providers: z.array(providerSettingResponseSchema),
})

export type SettingsResponse = z.infer<typeof settingsResponseSchema>

export const providerSettingRequestSchema = z.object({
  id: z.number().int(),
  isEnabled: z.boolean(),
  intervalMinutes: z.number().int(),
  requiresApiKey: z.boolean().optional(),
})

export type ProviderSettingRequest = z.infer<
  typeof providerSettingRequestSchema
>

export const settingsRequestSchema = z.object({
  logLevel: z.string(),
  providers: z.array(providerSettingRequestSchema),
})

export type SettingsRequest = z.infer<typeof settingsRequestSchema>

export const credentialStatusSchema = z.object({
  identity: z.string(),
  configured: z.boolean(),
  keyVersion: omittedNullableIntegerSchema,
  updatedAt: omittedNullableStringSchema,
})

export type CredentialStatus = z.infer<typeof credentialStatusSchema>

/* -------------------------------------------------------------------------- */
/*  AI Settings                                                              */
/* -------------------------------------------------------------------------- */

export const aiSettingsSchema = z.object({
  baseUrl: omittedNullableStringSchema,
  modelId: omittedNullableStringSchema,
  enabled: z.boolean(),
  apiKeyConfigured: z.boolean(),
})

export type AiSettings = z.infer<typeof aiSettingsSchema>

export const aiSettingsRequestSchema = z.object({
  baseUrl: z.string().nullish(),
  modelId: z.string().nullish(),
  enabled: z.boolean(),
})

export type AiSettingsRequest = z.infer<typeof aiSettingsRequestSchema>

export const iocChatSettingsSchema = z.object({
  baseUrl: omittedNullableStringSchema,
  modelId: omittedNullableStringSchema,
  enabled: z.boolean(),
  apiKeyConfigured: z.boolean(),
})

export type IocChatSettings = z.infer<typeof iocChatSettingsSchema>

export const iocChatSettingsRequestSchema = z.object({
  baseUrl: z.string().nullish(),
  modelId: z.string().nullish(),
  enabled: z.boolean(),
})

export type IocChatSettingsRequest = z.infer<typeof iocChatSettingsRequestSchema>

export const aiReportPolicyVersionSchema = z.object({
  version: z.number().int(),
  text: z.string(),
  authorUserId: omittedNullableStringSchema,
  publishedAt: omittedNullableStringSchema,
})

export type AiReportPolicyVersion = z.infer<
  typeof aiReportPolicyVersionSchema
>

export const aiReportPolicySchema = z.object({
  activeVersion: z.number().int(),
  text: z.string(),
  authorUserId: omittedNullableStringSchema,
  publishedAt: omittedNullableStringSchema,
  history: z.array(aiReportPolicyVersionSchema),
})

export type AiReportPolicy = z.infer<typeof aiReportPolicySchema>

export const aiReportPolicyPublishRequestSchema = z.object({
  text: z.string(),
  expectedVersion: z.number().int().nullish(),
})

export type AiReportPolicyPublishRequest = z.infer<
  typeof aiReportPolicyPublishRequestSchema
>

/* -------------------------------------------------------------------------- */
/*  Validation helpers                                                       */
/* -------------------------------------------------------------------------- */

export const supportedLogLevels = [
  'Verbose',
  'Debug',
  'Information',
  'Warning',
  'Error',
  'Fatal',
] as const

export type SupportedLogLevel = (typeof supportedLogLevels)[number]
