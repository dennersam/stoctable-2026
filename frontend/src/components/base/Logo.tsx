interface LogoProps {
  size?: number;
  className?: string;
}

export function Logo({ size = 28, className = '' }: LogoProps) {
  const height = Math.round(size * (61 / 70));
  return (
    <svg
      width={size}
      height={height}
      viewBox="0 0 70 61"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={className}
    >
      <path
        d="M2 11.4475V59M2 11.4475H54.237M2 11.4475L18.5782 2M2 59H54.237M2 59L18.5782 44.8287M54.237 59V11.4475M54.237 59L68 44.8287M54.237 11.4475L68 2M18.5782 2H68M18.5782 2V44.8287M68 2V44.8287M68 44.8287H18.5782"
        stroke="currentColor"
        strokeWidth="3"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
