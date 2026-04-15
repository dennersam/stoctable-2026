import api from '@/lib/api';
import type { AuthUser } from '@/types/auth';

export interface UpdateProfilePayload {
  fullName?: string;
  avatarUrl?: string | null;
  currentPassword?: string;
  newPassword?: string;
}

export interface UserProfileResponse {
  id: string;
  username: string;
  email: string;
  fullName: string;
  role: string;
  avatarUrl?: string | null;
}

export const profileService = {
  getMe: async (): Promise<UserProfileResponse> => {
    const response = await api.get<UserProfileResponse>('/users/me');
    return response.data;
  },

  updateMe: async (payload: UpdateProfilePayload): Promise<UserProfileResponse> => {
    const response = await api.put<UserProfileResponse>('/users/me', payload);
    return response.data;
  },
};

/** Converts a File to a base64 data URL, resizing to max 256×256. */
export function resizeImageToBase64(file: File, maxSize = 256): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);
    img.onload = () => {
      URL.revokeObjectURL(url);
      const scale = Math.min(1, maxSize / Math.max(img.width, img.height));
      const w = Math.round(img.width * scale);
      const h = Math.round(img.height * scale);
      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h);
      resolve(canvas.toDataURL('image/jpeg', 0.85));
    };
    img.onerror = reject;
    img.src = url;
  });
}

/** Patches an AuthUser with updated profile data. */
export function mergeProfileIntoUser(user: AuthUser, profile: UserProfileResponse): AuthUser {
  return { ...user, fullName: profile.fullName, avatarUrl: profile.avatarUrl };
}
