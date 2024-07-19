![Globo](img/banner.jpg)
- [Apresentação](#apresentação)
- [O problema](#o-problema)
  - [Capacidade de programar ou ajustar o código da API usando uma linguagem de programação](#capacidade-de-programar-ou-ajustar-o-código-da-api-usando-uma-linguagem-de-programação)
  - [Automação da infra, provisionamento dos hosts](#automação-da-infra-provisionamento-dos-hosts)
  - [Automação de setup e configuração dos hosts](#automação-de-setup-e-configuração-dos-hosts)
  - [Pipeline de CI/CD automatizado](#pipeline-de-cicd-automatizado)
  - [Monitoração básica da aplicação](#monitoração-básica-da-aplicação)
- [Caso tivesse mais tempo.](#caso-tivesse-mais-tempo)
- [Referências](#referências)
- [Autora](#autora)

## Apresentação

Olá, meu nome é Denize, eu continuo aprendendo sobre a cultura DevOps, atualmente trabalho como desenvolvedora Web, mas tenho interesse em me aprimorar em ferramentas DevOps.
Obrigada pela oportunidade e abaixo segue o conteúdo que produzi para o desafio.

## O problema

### Capacidade de programar ou ajustar o código da API usando uma linguagem de programação

Para criar uma API em .NET só precisa de um comando para iníciar:

```bash
dotnet new web #criar
dotnet watch run #roda e a cada alteração faz um hot reload
```

Pronto, já tenho a base para começar, e poderia mudar mas a porta foi configurada no 5042

![](/CommentsImages/1.jpg)

Posso ver o Hello World no navegador.
![](/CommentsImages/2.jpg)

O código é bem simples, uma lambda para salvar em uma lista em memória e um lambda para buscar o item:

```cs
app.MapPost("/api/comment/new", (Comment comment) => {
    commentsList.Add(comment);
    return TypedResults.Created("/api/comment/list/{id}", comment);
});

app.MapGet("/api/comment/list/{id}", Results<Ok<Comment>, NotFound> (int id) => {
    var commentFound = commentsList.FirstOrDefault(m => m.content_id == id);
    return  commentFound is null ? TypedResults.NotFound() : TypedResults.Ok(commentFound);
});
```

Usando o postman consegui localmente os resultados esperados:

![](/CommentsImages/3.jpg)

Agora para conteinerização desse código...

Seguindo alguns guias para criar o [Dockerfile da API em .Net](https://learn.microsoft.com/en-us/visualstudio/containers/container-build?view=vs-2022)

Além disso, tive que mudar algumas coisas no .csproj para conseguir fazer o container.

Para construir meu container:
````bash
docker build -t desafio_devops .
````

Para subir ele e testar localmente:
````bash
docker run --name desafioDevops -p 8080:8080 desafio_devops
````
Então agora se eu quiser ver meu container funcionando localmente posso acessar a porta que configurei...

### Automação da infra, provisionamento dos hosts 

Automação de infra vou usar Terraform, no provedor da Google, gosto de usar o terraform em um container deixado junto com o projeto. Para isso vou criar uma diretório chamado infra e criar minhas configurações lá.


Com a documentação do [terraform](https://registry.terraform.io/providers/hashicorp/google/latest/docs/resources/storage_bucket) e do [Google Cloud](https://cloud.google.com/docs/terraform/resource-management/store-state) na mão vou criar um container para usar meu terraform sem precisar instalar na minha máquina usando o compose.yml:

```yml
services:
  terraform:
    image: cedricguadalupe/terraform-gcloud #imagem com o cli da google cloud já instalado e terraform
    container_name: terraform
    stdin_open: true 
    tty: true 
    working_dir: /app
    entrypoint: ''
    command: sh
    volumes:
      - C:/0/Denize_Bassi/infra:/app #Coloquei meu volume aqui para conseguir lidar com o container sem ter que ficar transferindo arquivos, me ajuda a debugar também.
```
Agora um comando para subir baixar a imagem e subir o container:

```bash
docker compose up -d #a opção vai permitir rodar no background não ocupando meu terminal
```

Durante a entrevista o Fernando mencionou que usam muito o GCP, e eu não tinha tido ainda contato com a plataforma, então decidi fazer a infra lá e já ir treinando.
Criei minha conta, e um projeto:

![](/CommentsImages/4.jpg)

Fiz login na minha conta usando o seguinte comando dentro do container rodando o terraform e gcloud cli
```bash
docker exec -it terraform bash #entro no container para usar comandos bash diretamente nele

gcloud auth application-default login
#depois de logar 
gcloud config set project desafio-devops-429519 #configura o projeto 
```

Como o volume esta configurado para ser o mesmo caminho da pasta infra então consigo ver o recém criado main.tf que contém o seguinte código:

![](/CommentsImages/5.jpg)

````
provider "google" {
  project = "desafio-devops-429519"
  region  = "us-east1"
  zone    = "us-east1-b"
}

resource "random_id" "default" {
  byte_length = 8
}

resource "google_storage_bucket" "default" {
  name     = "${random_id.default.hex}-terraform-state"
  location = "US"

  force_destroy               = false
  public_access_prevention    = "enforced"
  uniform_bucket_level_access = true

  versioning {
    enabled = true
  }
}

resource "local_file" "default" {
  file_permission = "0644"
  filename        = "${path.module}/backend.tf"

  content = <<-EOT
  terraform {
    backend "gcs" {
      bucket = "${google_storage_bucket.default.name}"
    }
  }
  EOT
}
````

Agora só rodar os seguintes comando para o terraform conseguir criar o bucket:

```bash
terraform init
terraform plan -out setPlan #Salva em um arquivo as configurações que será enviado para o bucket na nuvem, e com isso podemos manter a infra disponível, distribuida e atualizada para todos que precisem.
terraform apply setPlan 
```

![](/CommentsImages/6.jpg)v

Agora salvar o primeiro state incializando um novo recurso, uma instância no compute engine, deixei tudo default exceto o firewall, que vou permitir acessar via http:

Peguei o código dentro da interface que usaria para fazer manualmente:
````
resource "google_compute_instance" "instance-20240717-033938" {
  boot_disk {
    auto_delete = true
    device_name = "instance-20240717-033938"

    initialize_params {
      image = "projects/debian-cloud/global/images/debian-12-bookworm-v20240709"
      size  = 10
      type  = "pd-balanced"
    }

    mode = "READ_WRITE"
  }

  can_ip_forward      = false
  deletion_protection = false
  enable_display      = false

  labels = {
    goog-ec-src = "vm_add-tf"
  }

  machine_type = "e2-medium"
  name         = "instance-20240717-033938"

  network_interface {
    access_config {
      network_tier = "PREMIUM"
    }

    queue_count = 0
    stack_type  = "IPV4_ONLY"
    subnetwork  = "projects/desafio-devops-429519/regions/us-east1/subnetworks/default"
  }

  scheduling {
    automatic_restart   = true
    on_host_maintenance = "MIGRATE"
    preemptible         = false
    provisioning_model  = "STANDARD"
  }

  service_account {
    email  = "494311237302-compute@developer.gserviceaccount.com"
    scopes = ["https://www.googleapis.com/auth/devstorage.read_only", "https://www.googleapis.com/auth/logging.write", "https://www.googleapis.com/auth/monitoring.write", "https://www.googleapis.com/auth/service.management.readonly", "https://www.googleapis.com/auth/servicecontrol", "https://www.googleapis.com/auth/trace.append"]
  }

  shielded_instance_config {
    enable_integrity_monitoring = true
    enable_secure_boot          = false
    enable_vtpm                 = true
  }

  tags = ["http-server"]
  zone = "us-east1-b"
}

````

![](/CommentsImages/7.jpg)

Como agora vou sincronizar com o bucket faço novamente os comandos do terraform já adicionando o novo recurso
```bash
terraform init
terraform plan -out setPlan 
terraform apply setPlan 
```

E tenho agora um state no meu bucket e minha instância criada

![](/CommentsImages/8.jpg)

### Automação de setup e configuração dos hosts

Após configurado o primeiro setup do terraform fui pesquisar do Ansible, eu nunca tinha utilizado, então estou experimetando utilizar diretamente em uma instância no gcp, para isso vi alguns [tutoriais na internet](https://mydevops353097059.wordpress.com/dockerize-a-net-core-application-with-ansible-2/) com [guias](https://devopsartisan.ro.digital/blog/ansible-google-cloud-platform) para me ajudar nessa empreitada. 

Alguns vídeos e varios testes depois, adicionei no meu terraform uma instância que vai usar o Ansible. Coloquei a configuração em um novo arquivo e adicionei um startup script que já vai atualizar minha VM e instalar o Ansible

O trecho abaixo é o que vai criar a instância e já instalar o Ansible:

````
metadata = {
    startup-script = "apt-get update \n apt-get install software-properties-common \n apt-add-repository ppa:ansible/ansible \n apt-get update \n apt-get -y install ansible"
  }
````
![](/CommentsImages/9.jpg)

Legal! Mesmo sendo meu primeiro acesso a VM, já está instalado o Ansible

Agora para configurar minha outra máquina vou precisar alterar meu arquivo host adicionando o IP gerado.

### Pipeline de CI/CD automatizado

### Monitoração básica da aplicação 

## Caso tivesse mais tempo.


## Referências

[Containerize a Python application](https://docs.docker.com/language/python/containerize/)  
[Ideia de In-house provider](https://davidstamen.com/2021/04/13/using-an-in-house-provider-with-terraform-v0.14/)  
[Terraform Essentials](https://www.linuxtips.io/course/terraform-essentials)


## Autora

**Denize**

It is not luck, it is hard work!

<img style="border-radius: 50%;" src="https://user-images.githubusercontent.com/46844031/163518939-915f6e15-200a-4e9c-9f54-9bee6beec89b.jpg" width="100px;" alt=""/>

Where to find me:


[![Twitter Badge](https://img.shields.io/badge/Twitter-1DA1F2?style=for-the-badge&logo=twitter&logoColor=white)](https://twitter.com/Dbassi91)   
[![Linkedin Badge](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/dbfigueiredo/)   
[![Gmail Badge](  https://img.shields.io/badge/Gmail-D14836?style=for-the-badge&logo=gmail&logoColor=white)](mailto:denize.f.bassi@gmail.com)   
[![CodePen](https://img.shields.io/badge/Codepen-000000?style=for-the-badge&logo=codepen&logoColor=white)](https://codepen.io/debafig)   
[![Facebook Badge](https://img.shields.io/badge/Facebook-1877F2?style=for-the-badge&logo=facebook&logoColor=white)](https://www.facebook.com/d.bassi91/)   
[![GitHub Badge](https://img.shields.io/badge/GitHub-100000?style=for-the-badge&logo=github&logoColor=white)](https://github.com/DeBaFig)   
[![Instagram Badge](https://img.shields.io/badge/Instagram-E4405F?style=for-the-badge&logo=instagram&logoColor=white)](https://www.instagram.com/bassidenize/)   
[![About.me Badge](https://img.shields.io/badge/website-000000?style=for-the-badge&logo=About.me&logoColor=white)](https://debafig.github.io/me/)   
[![Whatsapp](https://img.shields.io/badge/WhatsApp-25D366?style=for-the-badge&logo=whatsapp&logoColor=white)](https://whatsa.me/5547935051914)
[![Discord](https://img.shields.io/badge/DeBaFig%235875-%237289DA.svg?style=for-the-badge&logo=discord&logoColor=white)](https://discordapp.com/users/DeBaFig#5875)
