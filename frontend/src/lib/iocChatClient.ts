import { z } from 'zod'
import { apiFetch } from './apiClient'

const chatTurnSchema = z.object({
  role: z.enum(['user', 'assistant']),
  content: z.string(),
})

const iocChatResponseSchema = z.object({
  answer: z.string(),
  iocIds: z.array(z.number().int()),
  providerRecordIds: z.array(z.string()),
  hasAssessment: z.boolean(),
})

export type IocChatTurn = z.infer<typeof chatTurnSchema>
export type IocChatResponse = z.infer<typeof iocChatResponseSchema>

export function postIocChat(
  iocId: number,
  message: string,
  history: IocChatTurn[],
  signal?: AbortSignal,
): Promise<IocChatResponse> {
  return apiFetch(`/iocs/${iocId}/chat`, {
    method: 'POST',
    body: { message, history: history.slice(-8) },
    schema: iocChatResponseSchema,
    signal,
    allowUnauthorized: true,
  })
}