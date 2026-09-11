import { z } from 'zod'

export const iocSearchParamsSchema = z.object({
  q: z.string().trim().min(1).max(2048),
  tag: z.string().trim().min(1).max(100).optional(),
})

export type IocSearchParams = z.infer<typeof iocSearchParamsSchema>
