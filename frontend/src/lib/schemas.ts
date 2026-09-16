import { z } from 'zod'
import { apiProblemSchema } from './apiErrors'

export { apiProblemSchema }

export const iocTypeTokens = ['ipv4', 'ipv6', 'domain', 'url'] as const
export type IocTypeToken = (typeof iocTypeTokens)[number]

export const iocSearchParamsSchema = z.object({
  q: z.string().trim().min(1).max(2048),
  tag: z.string().trim().min(1).max(100).optional(),
})

export type IocSearchParams = z.infer<typeof iocSearchParamsSchema>

/**
 * UTC instant as serialized by the API. `DateTime` values may omit the offset
 * suffix, so consumers must parse them with `parseUtcDateTime` to treat them
 * as UTC deterministically across server and client.
 */
export const utcInstantSchema = z.string().min(1)

/**
 * One provider row's evidence inside an IOC group. Provider-inapplicable fields
 * are omitted by the API's null-ignoring serializer, so every typed field is
 * optional; stored false/0/empty values remain present and must be kept.
 */
export const iocSourceSchema = z.object({
  provider: z.string(),
  providerRecordId: z.string(),
  providerDetailsLink: z.string().nullable().optional(),
  port: z.number().int().nullable().optional(),
  firstSeenUtc: utcInstantSchema,
  lastSeenUtc: utcInstantSchema.nullable().optional(),
  threatType: z.string().nullable().optional(),
  malware: z.string().nullable().optional(),
  malwareAlias: z.string().nullable().optional(),
  malwarePrintable: z.string().nullable().optional(),
  isCompromised: z.boolean().nullable().optional(),
  reporter: z.string().nullable().optional(),
  anonymous: z.boolean().nullable().optional(),
  isVerified: z.boolean().nullable().optional(),
  target: z.string().nullable().optional(),
  phishTankDetails: z.unknown().optional(),
  phishTankOnline: z.boolean().nullable().optional(),
  threatFoxConfidenceLevel: z.number().int().nullable().optional(),
  tags: z.array(z.string()),
  isFalsePositive: z.boolean(),
})

export type IocSource = z.infer<typeof iocSourceSchema>

/** One normalized IOC group as returned by every query endpoint. */
export const iocGroupSchema = z.object({
  id: z.number().int(),
  iocValue: z.string(),
  iocType: z.enum(iocTypeTokens),
  urlHost: z.string().nullable().optional(),
  parentDomain: z.string().nullable().optional(),
  firstSeenUtc: utcInstantSchema,
  lastSeenUtc: utcInstantSchema.nullable().optional(),
  sources: z.array(iocSourceSchema),
})

export type IocGroup = z.infer<typeof iocGroupSchema>

/** Shared envelope for the multi-item search, browse, and relations endpoints. */
export const pagedIocGroupsSchema = z.object({
  items: z.array(iocGroupSchema),
  page: z.number().int(),
  pageSize: z.number().int(),
  totalCount: z.number().int(),
})

/** Public provider catalog used by browse column filters. */
export const iocProviderNamesSchema = z.array(z.string().min(1))

export type PagedIocGroups = z.infer<typeof pagedIocGroupsSchema>

/** `POST /api/auth/login` success response; role is `User` or `Admin`. */
export const loginResponseSchema = z.object({
  token: z.string().min(1),
  username: z.string().min(1),
  role: z.enum(['User', 'Admin']),
  expiresAt: z.string().min(1),
})

export type LoginResponse = z.infer<typeof loginResponseSchema>
