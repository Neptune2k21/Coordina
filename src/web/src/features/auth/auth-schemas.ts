import * as z from "zod"

const passwordSchema = z
  .string()
  .min(8, "Password must be at least 8 characters.")

export const loginSchema = z.object({
  name: z.string(),
  email: z.string().email("Enter a valid email."),
  password: passwordSchema,
})

export const registerSchema = loginSchema.extend({
  name: z.string().trim().min(2, "Name must be at least 2 characters."),
})
