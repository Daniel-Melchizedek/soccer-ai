const copies = {
  'en-GB': {
    heading: 'Match intelligence', view: 'Insight view', casual: 'Casual', analyst: 'Analyst',
    recap: 'Recap', refresh: 'Refresh recap',
    scopes: { recent: 'Last two minutes', match: 'Match so far', halftime: 'Half-time', fulltime: 'Full-time' },
    columns: ['Team', 'Shots', 'Goals', 'Corners'],
    counts: 'Event counts in selected recap period', comparison: 'Shots in selected period',
    disclosure: 'Synthetic match evidence. Fictional teams and players. Server event descriptions, explanations and limitations remain in English.',
    unavailable: 'Match facts unavailable. Retry with Refresh recap.', updating: 'Updating match facts...',
    clock: (clock, scope) => `Match clock ${clock}. ${scope}.`,
    empty: 'No goals, shots, cards, substitutions or period endings in this interval.',
    source: 'Source event',
    evidence: (eventId, period, clock) => `${eventId}; half ${period}; clock ${clock} ms`,
    shots: count => `${count} shots`, shotsLabel: team => `${team} shots`,
    analystNote: (selected, total, matchId) => `Showing ${selected} of ${total} highlights. Match ID: ${matchId}.`,
    casualNote: (selected, recent) => `Score at the end of this interval. ${recent ? 'The interval stays within the current half. ' : ''}Showing ${selected} selected moments.`
  },
  'es-ES': {
    heading: 'Análisis del partido', view: 'Vista del análisis', casual: 'General', analyst: 'Analista',
    recap: 'Resumen', refresh: 'Actualizar resumen',
    scopes: { recent: 'Últimos dos minutos', match: 'Partido hasta ahora', halftime: 'Descanso', fulltime: 'Final del partido' },
    columns: ['Equipo', 'Tiros', 'Goles', 'Córneres'],
    counts: 'Recuento de eventos en el período seleccionado', comparison: 'Tiros en el período seleccionado',
    disclosure: 'Datos de un partido simulado. Equipos y jugadores ficticios. Traducción de la interfaz al español en borrador; requiere revisión por hablantes nativos. Las descripciones de eventos, explicaciones y limitaciones del servidor siguen en inglés.',
    unavailable: 'Datos del partido no disponibles. Vuelve a intentarlo con Actualizar resumen.', updating: 'Actualizando datos del partido...',
    clock: (clock, scope) => `Tiempo de partido ${clock}. ${scope}.`,
    empty: 'No hay goles, tiros, tarjetas, sustituciones ni finales de período en este intervalo.',
    source: 'Evento de origen',
    evidence: (eventId, period, clock) => `${eventId}; parte ${period}; tiempo ${clock} ms`,
    shots: count => `${count} tiros`, shotsLabel: team => `Tiros de ${team}`,
    analystNote: (selected, total, matchId) => `Se muestran ${selected} de ${total} eventos destacados. ID del partido: ${matchId}.`,
    casualNote: (selected, recent) => `Marcador al final de este intervalo. ${recent ? 'El intervalo se limita a la parte actual. ' : ''}Se muestran ${selected} momentos seleccionados.`
  },
  'ta-IN': {
    heading: 'போட்டி பகுப்பாய்வு', view: 'பகுப்பாய்வுக் காட்சி', casual: 'எளிய காட்சி', analyst: 'விரிவான பகுப்பாய்வு',
    recap: 'சுருக்கம்', refresh: 'சுருக்கத்தைப் புதுப்பி',
    scopes: { recent: 'கடைசி இரண்டு நிமிடங்கள்', match: 'இதுவரையிலான போட்டி', halftime: 'இடைவேளை', fulltime: 'முழு நேரம்' },
    columns: ['அணி', 'ஷாட்கள்', 'கோல்கள்', 'கார்னர்கள்'],
    counts: 'தேர்ந்தெடுத்த காலப்பகுதியின் நிகழ்வு எண்ணிக்கைகள்', comparison: 'தேர்ந்தெடுத்த காலப்பகுதியின் ஷாட்கள்',
    disclosure: 'உருவகப்படுத்தப்பட்ட போட்டித் தரவுகள். கற்பனை அணிகள் மற்றும் வீரர்கள். தமிழ் இடைமுக மொழிபெயர்ப்பு வரைவு நிலையில் உள்ளது; தாய்மொழிப் பேச்சாளர்களின் மதிப்பாய்வு தேவை. சேவையக நிகழ்வு விவரங்கள், விளக்கங்கள் மற்றும் வரம்புகள் ஆங்கிலத்திலேயே உள்ளன.',
    unavailable: 'போட்டித் தகவல்கள் கிடைக்கவில்லை. சுருக்கத்தைப் புதுப்பி பொத்தானை அழுத்தி மீண்டும் முயற்சிக்கவும்.', updating: 'போட்டித் தகவல்கள் புதுப்பிக்கப்படுகின்றன...',
    clock: (clock, scope) => `போட்டி நேரம் ${clock}. ${scope}.`,
    empty: 'இந்தக் காலப்பகுதியில் கோல்கள், ஷாட்கள், அட்டைகள், வீரர் மாற்றங்கள் அல்லது ஆட்டப்பகுதி முடிவுகள் இல்லை.',
    source: 'மூல நிகழ்வு',
    evidence: (eventId, period, clock) => `${eventId}; பாதி ${period}; நேரம் ${clock} ms`,
    shots: count => `${count} ஷாட்கள்`, shotsLabel: team => `${team} ஷாட்கள்`,
    analystNote: (selected, total, matchId) => `${total} முக்கிய நிகழ்வுகளில் ${selected} காட்டப்படுகின்றன. போட்டி ID: ${matchId}.`,
    casualNote: (selected, recent) => `இந்தக் காலப்பகுதியின் முடிவில் உள்ள ஸ்கோர். ${recent ? 'காலப்பகுதி தற்போதைய பாதிக்குள் மட்டுமே உள்ளது. ' : ''}தேர்ந்தெடுத்த ${selected} தருணங்கள் காட்டப்படுகின்றன.`
  }
};

export function recapCopy(language) {
  const locale = { en: 'en-GB', es: 'es-ES', ta: 'ta-IN' }[language] ?? language;
  return { language: copies[locale] ? locale : 'en-GB', copy: copies[locale] ?? copies['en-GB'] };
}