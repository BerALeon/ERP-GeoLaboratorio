import type { ThemeConfig } from 'antd'

export const colors = {
  primary: '#0A2A8F',   // azul corporativo
  dark: '#2B2F36',      // gris oscuro
  light: '#EEF0F3',     // gris claro
  border: '#D5D9DF',
  ok: '#2E7D32', warn: '#B26A00', bad: '#B71C1C',
}

/** Tema minimalista e industrial: sin sombras exageradas, radios pequeños, sin animaciones decorativas. */
export const theme: ThemeConfig = {
  token: {
    colorPrimary: colors.primary,
    colorTextBase: colors.dark,
    colorBgLayout: colors.light,
    colorBorder: colors.border,
    borderRadius: 3,
    fontFamily: "'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
    boxShadow: 'none',
    boxShadowSecondary: '0 1px 4px rgba(0,0,0,0.12)',
    motion: false,
  },
  components: {
    Layout: { headerBg: '#fff', siderBg: colors.dark, bodyBg: colors.light },
    Menu: { darkItemBg: colors.dark, darkItemSelectedBg: colors.primary },
    Table: { headerBg: colors.light, headerColor: colors.dark },
    Card: { headerFontSize: 14 },
  },
}
