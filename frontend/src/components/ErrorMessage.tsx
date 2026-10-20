export function ErrorMessage({ message }: { message: string | null }) {
  if (message === null) return null
  return (
    <p className="error" role="alert">
      {message}
    </p>
  )
}
