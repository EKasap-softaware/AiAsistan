function Rab([string]$yol) {
    $e = EnvOku
    $r = Calistir 'curl.exe' @('-s', '-m', '10', '-u', ($e['RABBITMQ_USER'] + ':' + $e['RABBITMQ_PASSWORD']), "http://localhost:15672/api/$yol") $Kok 20
    try { return ($r.Cikti | ConvertFrom-Json) } catch { return $null }
}
function Lab05 {
    $e = EnvOku
    if (-not $e['RABBITMQ_USER']) { Kontrol 'ön koşul: .env içinde RABBITMQ_USER/PASSWORD' $false '.env.example''ı kopyalayıp .env yap'; return }
    if ($null -eq (Rab 'overview')) { Kontrol 'ön koşul: RabbitMQ yönetim API''si (15672) cevap veriyor' $false 'docker compose up -d rabbitmq'; return }

    $q = Rab 'queues/%2F/atolye.kuyruk'
    Kontrol 'G1 kuyruk atolye.kuyruk: durable + dead-letter exchange = atolye.olu' `
        ($q -and $q.durable -eq $true -and $q.arguments.'x-dead-letter-exchange' -eq 'atolye.olu') `
        'Panel > Queues > Add: Name atolye.kuyruk, Durability Durable, Arguments: x-dead-letter-exchange = atolye.olu (String)'

    $x = Rab 'exchanges/%2F/atolye.olu'
    $dlq = Rab 'queues/%2F/atolye.olu-kuyruk'
    $bag = Rab 'exchanges/%2F/atolye.olu/bindings/source'
    Kontrol 'G2 exchange atolye.olu (fanout) + atolye.olu-kuyruk kuyruğu ona bağlı' `
        ($x -and $dlq -and (@($bag | Where-Object { $_.destination -eq 'atolye.olu-kuyruk' }).Count -ge 1)) `
        'Exchanges > Add (fanout) atolye.olu ; Queues > Add atolye.olu-kuyruk ; Exchange sayfasında Bindings > To queue atolye.olu-kuyruk'

    $yayin = if ($q.message_stats) { [int]$q.message_stats.publish } else { 0 }
    Kontrol ("G3 atolye.kuyruk'a en az 3 mesaj yayınladın (publish: {0}; sayaçlar 5-10 sn gecikir)" -f $yayin) ($yayin -ge 3) `
        'amq.default exchange > Publish message: Routing key = atolye.kuyruk, birkaç kez  (veya kuyruk sayfasında Publish message)'
    $ack = if ($q.message_stats) { [int]$q.message_stats.ack + [int]$q.message_stats.get_no_ack } else { 0 }   # panelde 'Ack message requeue false' = get_no_ack olarak sayılır
    Kontrol ("G4 en az 1 mesajı tüketip ACK ettin (ack: {0})" -f $ack) ($ack -ge 1) 'atolye.kuyruk > Get messages > Ack Mode: "Ack message requeue false" (Messages: 1)'
    Kontrol ("G5 bir mesajı REDDEDİP (reject, requeue=false) dead-letter kuyruğuna düşürdün (olu-kuyruk: {0} mesaj)" -f [int]$dlq.messages) ($dlq -and [int]$dlq.messages -ge 1) `
        'atolye.kuyruk > Get messages > Ack Mode: "Reject requeue false" -> mesaj atolye.olu-kuyruk''a gider'
}
